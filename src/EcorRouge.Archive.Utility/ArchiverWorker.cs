using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ionic.Zip;
using log4net;
using EcorRouge.Archive.Utility.Extensions;
using EcorRouge.Archive.Utility.Plugins;
using EcorRouge.Archive.Utility.Settings;

namespace EcorRouge.Archive.Utility
{
    public enum ArchiverState
    {
        Initializing,
        ErrorStarting,
        Archiving,
        Uploading,
        UploadWaiting,
        Deleting,
        Completed
    }

    public class ArchiverWorker
    {
        internal static readonly ILog log = LogManager.GetLogger(typeof(ArchiverWorker));

        private const int MAX_WAIT_TIME = 60;

        private string _fileName;
        private PluginBase _plugin;
        private Dictionary<string, object> _pluginProperties;
        private int _maximumFiles;
        private int _maximumArchiveSizeMb;

        private CancellationTokenSource _cts;

        private long _filesInArchive = 0;
        private long _filesSizeInArchive = 0;
        private long _filesProcessed = 0;
        private long _bytesProcessed = 0;

        private long _bytesUploaded = 0;
        private double _uploadProgress = 0;

        private StreamWriter _skippedListFile;
        private string _metaFileName;
        private StreamWriter _metaFile;
        private ZipOutputStream _zipFile;
        private string _zipFileName;
        private List<string> _archiveFileList;
        private long _deletedFilesCount;

        public EventHandler Completed;
        public EventHandler ArchivingProgress;
        public EventHandler UploadingProgress;
        public EventHandler DeletingProgress;
        public EventHandler StateChanged;

        public bool IsBusy { get; private set; }

        public long FilesInArchive => _filesInArchive;
        public long FilesSizeInArchive => _filesSizeInArchive;
        public long ArchiveSize => _zipFile?.Position ?? 0;
        public long DeletedFilesCount => _deletedFilesCount;
        public long FilesProcessed => _filesProcessed;
        public long BytesProcessed => _bytesProcessed;
        public long BytesUploaded => _bytesUploaded;
        public double UploadProgress => _uploadProgress;

        public int SecondsBeforeRetry { get; set; }

        public double ArchiveFileProgress { get; private set; }

        public ArchiverState State { get; private set; }

        public ArchiverWorker(string fileName, PluginBase plugin, Dictionary<string, object> pluginProperties, int maximumFiles, int maximumArchiveSizeMb)
        {
            this._fileName = fileName;
            this._plugin = plugin;
            this._pluginProperties = pluginProperties;
            this._maximumFiles = maximumFiles;
            this._maximumArchiveSizeMb = maximumArchiveSizeMb;

            _plugin.OnLogMessage += ArchiverPlugin_OnLogMessage;
            _plugin.OnUploadProgress += ArchiverPlugin_OnUploadProgress;
        }

        private void ArchiverPlugin_OnUploadProgress(long bytesUploaded, double percentUploaded)
        {
            _bytesUploaded = bytesUploaded;
            _uploadProgress = percentUploaded;

            UploadingProgress?.Invoke(this, EventArgs.Empty);
        }

        private void ArchiverPlugin_OnLogMessage(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Error:
                    log.Error(message);
                    break;
                case LogLevel.Info:
                    log.Info(message);
                    break;
                case LogLevel.Warn:
                    log.Warn(message);
                    break;
                default:
                    log.Debug(message);
                    break;
            }
        }

        public void Cancel()
        {
            _cts?.Cancel();
        }

        public void Start()
        {
            _filesInArchive = 0;
            _filesSizeInArchive = 0;
            _filesProcessed = 0;
            _bytesProcessed = 0;

            ArchiveFileProgress = 0;

            _cts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                await DoWork(_cts.Token);
            }).ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    log.Error("Error in DoWork", t.Exception);
                }

                Completed?.Invoke(this, EventArgs.Empty);
            });
        }

        private void ChangeState(ArchiverState state)
        {
            if (State == state)
                return;

            State = state;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private async Task DoWork(CancellationToken cancellationToken)
        {
            IsBusy = true;

            ChangeState(ArchiverState.Initializing);

            try
            {
                await _plugin.TryConnectAndWriteSmallFile(_pluginProperties, cancellationToken);
            }
            catch (Exception ex)
            {
                log.Error($"Error testing credentials: {ex.Message}", ex);

                ChangeState(ArchiverState.ErrorStarting);
                IsBusy = false;
                return;
            }

            try
            {
                _skippedListFile = new StreamWriter(Path.Combine(PathHelper.GetRootDataPath(), "skipped_files.txt"));

                if (_plugin.KeepSession)
                {
                    await _plugin.OpenSessionAsync(_pluginProperties, cancellationToken);
                }

                OpenZipFile();

                using var reader = new StreamReader(_fileName);

                while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                {
                    var line = reader.ReadLine();

                    if (String.IsNullOrWhiteSpace(line))
                        continue;

                    //{file.FileName}|{file.FileLength}|{file.LastWriteTime}
                    var parts = line.Split("|");
                    if (parts.Length < 3)
                        continue;

                    ChangeState(ArchiverState.Archiving);

                    try
                    {
                        ArchiveFileProgress = 0;
                        ProcessFile(parts[0], cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _skippedListFile.WriteLine(line);
                        log.Error("Error processing file", ex);
                    }

                    _filesProcessed++;
                    _filesInArchive++;

                    ArchivingProgress?.Invoke(this, EventArgs.Empty);

                    if (ShouldFlushZip())
                    {
                        await FlushArchive(cancellationToken);

                        OpenZipFile();

                        ArchiveFileProgress = 0;
                        ArchivingProgress?.Invoke(this, EventArgs.Empty);
                    }
                }

                await FlushArchive(cancellationToken);
            }
            catch (Exception ex)
            {
                log.Error("Error in Progress.DoWork()", ex);
            }
            finally
            {
                if (_plugin.KeepSession)
                {
                    try
                    {
                        await _plugin.CloseSessionAsync(cancellationToken);
                    }
                    catch (Exception) {/* ignore */}
                }

                try
                {
                    if (_skippedListFile != null)
                    {
                        _skippedListFile.Close();
                        await _skippedListFile.DisposeAsync();
                    }
                }
                catch (Exception) {/* ignore */}

                ChangeState(ArchiverState.Completed);
                IsBusy = false;
            }
        }

        private void ProcessFile(string fileName, CancellationToken cancellationToken)
        {
            if (!File.Exists(fileName))
                throw new FileNotFoundException("File not found", fileName);

            var fInfo = new FileInfo(fileName);

            var guid = Guid.NewGuid();
            var newFileName = guid + Path.GetExtension(fileName);

            _metaFile.WriteLine($"{newFileName}|{fInfo.Length}|{fInfo.CreationTime.ToUnixTime()}|{fileName}");

            _zipFile.PutNextEntry(newFileName);
            WriteFileStream(fileName, fInfo.Length, cancellationToken);

            _archiveFileList.Add(fileName);
            _bytesProcessed += fInfo.Length;
        }

        private bool ShouldFlushZip()
        {
            return _filesInArchive > _maximumFiles || _zipFile.Position / (1024 * 1024) > _maximumArchiveSizeMb;
        }

        private void OpenZipFile()
        {
            var guid = Guid.NewGuid();

            _archiveFileList = new List<string>();

            _metaFileName = Path.Combine(PathHelper.GetTempPath(true), guid + ".txt");
            _metaFile = new StreamWriter(_metaFileName, false, Encoding.UTF8);

            _zipFileName = Path.Combine(PathHelper.GetTempPath(), guid + ".zip");

            _zipFile = new ZipOutputStream(_zipFileName);
            _zipFile.EnableZip64 = Zip64Option.Always;

            _filesInArchive = 0;
            _filesSizeInArchive = 0;

            ArchivingProgress?.Invoke(this, EventArgs.Empty);
        }

        private void WriteFileStream(string fileName, long totalSize, CancellationToken cancellationToken)
        {
            using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read);

            var buf = new byte[4 * 1024 * 1024];
            int bytesRead = 0;
            long totalBytes = 0;

            while ((bytesRead = stream.Read(buf, 0, buf.Length)) > 0 && !cancellationToken.IsCancellationRequested)
            {
                _zipFile.Write(buf, 0, bytesRead);
                totalBytes += bytesRead;

                ArchiveFileProgress = totalBytes * 100.0 / totalSize;
                ArchivingProgress?.Invoke(this, EventArgs.Empty);
            }
        }

        private async Task UploadArchiveWithRetries(CancellationToken cancellationToken)
        {
            int retryAttempts = 0;

            bool success = false;
            while (!cancellationToken.IsCancellationRequested && !success)
            {
                if (retryAttempts > 0)
                {
                    SecondsBeforeRetry = retryAttempts * 5;
                    if (SecondsBeforeRetry > MAX_WAIT_TIME)
                        SecondsBeforeRetry = MAX_WAIT_TIME;
                    
                    ChangeState(ArchiverState.UploadWaiting);
                    while (SecondsBeforeRetry > 0 && !cancellationToken.IsCancellationRequested)
                    {
                        SecondsBeforeRetry--;
                        UploadingProgress?.Invoke(this, EventArgs.Empty);
                        Thread.Sleep(1000);
                    }
                }

                if(cancellationToken.IsCancellationRequested)
                    return;

                ChangeState(ArchiverState.Uploading);

                try
                {
                    if (!_plugin.KeepSession)
                    {
                        await _plugin.OpenSessionAsync(_pluginProperties, cancellationToken);
                    }

                    try
                    {
                        await _plugin.UploadFileAsync(_zipFileName, cancellationToken);
                        success = true;
                    }
                    finally
                    {
                        if (!_plugin.KeepSession)
                        {
                            await _plugin.CloseSessionAsync(cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!success) // let's ignore errors in close session
                    {
                        log.Error("Error uploading archive", ex);
                        retryAttempts++;
                    }
                }
            }
        }

        private async Task FlushArchive(CancellationToken cancellationToken)
        {
            _metaFile.Close();
            await _metaFile.DisposeAsync();

            _zipFile.PutNextEntry(Path.GetFileName(_metaFileName));
            WriteFileStream(_metaFileName, new FileInfo(_metaFileName).Length, cancellationToken);

            _zipFile.Close();
            await _zipFile.DisposeAsync();

            // upload
            if (!cancellationToken.IsCancellationRequested)
            {
                await UploadArchiveWithRetries(cancellationToken);
            }

            // delete meta file
            try
            {
                File.Delete(_metaFileName);
            }
            catch (Exception ex)
            {
                log.Error("Error deleting meta file", ex);
            }

            // delete archive
            try
            {
                File.Delete(_zipFileName);
            }
            catch (Exception ex)
            {
                log.Error("Error deleting archive", ex);
            }

            if (cancellationToken.IsCancellationRequested)
                return;

            // delete source files
            ChangeState(ArchiverState.Deleting);

            _deletedFilesCount = 0;
            DeletingProgress?.Invoke(this, EventArgs.Empty);

            foreach (var fileName in _archiveFileList)
            {
                try
                {
                    File.Delete(fileName);
                }
                catch (Exception) {/* ignore */}

                _deletedFilesCount++;

                DeletingProgress?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
