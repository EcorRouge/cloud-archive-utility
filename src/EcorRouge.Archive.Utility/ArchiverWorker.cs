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
using EcorRouge.Archive.Utility.Util;

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

        private PluginBase _plugin;
        private Dictionary<string, object> _pluginProperties;

        private SavedState _savedState;

        private CancellationTokenSource _cts;

        private long _filesInArchive = 0;
        private long _filesSizeInArchive = 0;
        private long _filesProcessed = 0;
        private long _bytesProcessed = 0;

        private long _totalArchiveSize = 0;
        private long _zipFileSize = 0;
        private long _bytesUploaded = 0;
        private double _uploadProgress = 0;

        private string _manifestFileName;
        private StreamWriter _manifestWriter;
        private StreamWriter _skippedListFile;
        private string _metaFileName;
        private StreamWriter _metaFile;
        private ZipOutputStream _zipFile;
        private string _zipFileName;
        private InputFile _inputFile;
        private List<string> _archiveFileList;
        private long _deletedFilesCount;

        public EventHandler Completed;
        public EventHandler ArchivingProgress;
        public EventHandler UploadingProgress;
        public EventHandler DeletingProgress;
        public EventHandler StateChanged;

        public bool IsCanceled { get; private set; }
        public bool IsBusy { get; private set; }

        public long FilesInArchive => _filesInArchive;
        public long FilesSizeInArchive => _filesSizeInArchive;
        public long TotalArchiveSize => _totalArchiveSize;
        public long ArchiveSize => _zipFileSize;
        public long DeletedFilesCount => _deletedFilesCount;
        public long FilesProcessed => _filesProcessed;
        public long BytesProcessed => _bytesProcessed;
        public long BytesUploaded => _bytesUploaded;
        public double UploadProgress => _uploadProgress;

        public int SecondsBeforeRetry { get; set; }

        public double ArchiveFileProgress { get; private set; }

        public ArchiverState State { get; private set; }

        public ArchiverWorker(PluginBase plugin, Dictionary<string, object> pluginProperties, SavedState savedState, InputFile inputFile)
        {
            this._plugin = plugin;
            this._inputFile = inputFile;
            this._pluginProperties = pluginProperties;
            _savedState = savedState;

            if (_savedState.IsEmpty)
            {
                _savedState.SetPluginProperties(pluginProperties);
                _pluginProperties = pluginProperties;
            }
            else
            {
                _pluginProperties = _savedState.GetPluginProperties();
            }

            if (_plugin != null)
            {
                _plugin.OnLogMessage += ArchiverPlugin_OnLogMessage;
                _plugin.OnUploadProgress += ArchiverPlugin_OnUploadProgress;
            }
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
            _totalArchiveSize = 0;
            ArchiveFileProgress = 0;
            IsCanceled = false;

            _filesProcessed = _savedState.FilesProcessed;
            _bytesProcessed = _savedState.BytesProcessed;
            _totalArchiveSize = _savedState.TotalArchivedSize;

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

                IsCanceled = _cts.IsCancellationRequested;

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

            if (!_savedState.IsEmpty)
            {
                _manifestFileName = _savedState.ManifestFileName;
                _manifestWriter = new StreamWriter(_manifestFileName, true, Encoding.UTF8);
            }
            else
            {
                _manifestFileName = Path.Combine(PathHelper.GetTempPath(true), $"_manifest-{DateTime.Now:yyyy-MM-dd-HH-mm}.txt");

                if (File.Exists(_manifestFileName))
                {
                    File.Delete(_manifestFileName);
                }

                _manifestWriter = new StreamWriter(_manifestFileName, false, Encoding.UTF8);
            }

            _savedState.ManifestFileName = _manifestFileName;
            _savedState.Save();

            if (_plugin != null)
            {
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
            }

            try
            {
                using var parser = InputFileParser.OpenFile(_inputFile);

                _skippedListFile = new StreamWriter(Path.Combine(PathHelper.GetRootDataPath(), "skipped_files.txt"), !_savedState.IsEmpty);

                if (_plugin?.KeepSession ?? false)
                {
                    await _plugin.OpenSessionAsync(_pluginProperties, cancellationToken);
                }

               
                if(_plugin == null)
                {
                    _filesInArchive = _savedState.TotalFilesToArchive;
                    _deletedFilesCount = 0;
                }

                if (!_savedState.IsEmpty) // Let's skip processed files
                {
                    if (!String.IsNullOrEmpty(_savedState.ArchiveFileName) && File.Exists(_savedState.ArchiveFileName))
                    {
                        try
                        {
                            File.Delete(_savedState.ArchiveFileName);
                        }
                        catch (Exception ex)
                        {
                            log.Error($"Error removing {_savedState.ArchiveFileName}", ex);
                        }
                    }

                    try
                    {
                        while (parser.GetNextEntry() != null && !cancellationToken.IsCancellationRequested &&
                               _filesProcessed >= 0)
                        {
                            _filesProcessed--;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Error rewinding {_savedState.InputFileName} to {_savedState.FilesProcessed} line", ex);
                    }

                    _filesProcessed = _savedState.FilesProcessed;
                }

                InputFileEntry entry;

                while ((entry = parser.GetNextEntry()) != null && !cancellationToken.IsCancellationRequested)
                {
                    if (_plugin != null && _zipFile == null)
                    {
                        OpenZipFile();
                    }

                    ChangeState(_plugin == null ? ArchiverState.Deleting : ArchiverState.Archiving);

                    try
                    {
                        ArchiveFileProgress = 0;
                        ProcessFile(entry.Path, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _skippedListFile.WriteLine(entry.Path);
                        log.Error($"Error processing file: {entry.Path}", ex);
                    }

                    _filesProcessed++;
                    _filesInArchive++;

                    ArchivingProgress?.Invoke(this, EventArgs.Empty);

                    if (_plugin != null)
                    {
                        if (ShouldFlushZip())
                        {
                            await FlushArchive(cancellationToken);

                            ArchiveFileProgress = 0;
                            ArchivingProgress?.Invoke(this, EventArgs.Empty);
                        }
                    }

                    //Thread.Sleep(1000); //TODO: for test
                }

                if (_plugin != null)
                {
                    await FlushArchive(cancellationToken);
                }

                await UploadManifest(cancellationToken);

                SavedState.Clear();
            }
            catch (Exception ex)
            {
                log.Error("Error in Progress.DoWork()", ex);
            }
            finally
            {
                if (_plugin?.KeepSession ?? false)
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

            if (_plugin == null) // We are deleting
            {
                var length = fInfo.Length;

                File.Delete(fileName);

                _deletedFilesCount++;
                DeletingProgress?.Invoke(this, EventArgs.Empty);
                _bytesProcessed += length;
                return;
            }

            var guid = Guid.NewGuid();
            var newFileName = guid + Path.GetExtension(fileName);

            _metaFile.WriteLine($"{newFileName}|{fInfo.Length}|{fInfo.CreationTime.ToUnixTime()}|{fileName}");

            _manifestWriter.WriteLine($"{fileName}|{fInfo.Length}|{fInfo.CreationTime.ToUnixTime()}|{Path.GetFileName(_zipFileName)}|{newFileName}");

            _zipFile.PutNextEntry(newFileName);
            WriteFileStream(fileName, fInfo.Length, cancellationToken);

            _archiveFileList.Add(fileName);
            _bytesProcessed += fInfo.Length;
        }

        private bool ShouldFlushZip()
        {
            return _filesInArchive >= _savedState.MaximumFiles || _zipFile.Position / (1024 * 1024) >= _savedState.MaximumArchiveSizeMb;
        }

        private void OpenZipFile()
        {
            var guid = Guid.NewGuid();

            _archiveFileList = new List<string>();

            _metaFileName = Path.Combine(PathHelper.GetTempPath(true), guid + ".txt");
            _metaFile = new StreamWriter(_metaFileName, false, Encoding.UTF8);

            _zipFileName = Path.Combine(PathHelper.GetTempPath(), guid + ".zip");

            _savedState.BytesProcessed = BytesProcessed;
            _savedState.FilesProcessed = FilesProcessed;
            _savedState.ArchiveFileName = _zipFileName;
            _savedState.TotalArchivedSize = _totalArchiveSize;
            _savedState.Save();

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

                _filesSizeInArchive += bytesRead;
                _zipFileSize = _zipFile.Position;

                ArchiveFileProgress = totalBytes * 100.0 / totalSize;
                ArchivingProgress?.Invoke(this, EventArgs.Empty);
            }
        }

        private async Task UploadArchiveWithRetries(string zipFileName, CancellationToken cancellationToken)
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
                        await _plugin.UploadFileAsync(zipFileName, cancellationToken);
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

        private async Task UploadManifest(CancellationToken cancellationToken)
        {
            _manifestWriter.Close();

            var zipFileName = Path.Combine(PathHelper.GetTempPath(), Path.GetFileNameWithoutExtension(_manifestFileName) + ".zip");

            using (var zip = new ZipFile())
            {
                zip.UseZip64WhenSaving = Zip64Option.Always;
                zip.AddFile(_manifestFileName, "");
                zip.Save(zipFileName);
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await UploadArchiveWithRetries(zipFileName, cancellationToken);
            }

            try
            {
                File.Delete(_manifestFileName);
            }
            catch (Exception ex)
            {
                log.Error("Error deleting manifest", ex);
            }
            try
            {
                File.Delete(zipFileName);
            }
            catch (Exception ex)
            {
                log.Error("Error deleting manifest archive", ex);
            }
        }

        private async Task FlushArchive(CancellationToken cancellationToken)
        {
            if (_zipFile == null)
                return; // Already flushed

            _metaFile.Close();
            await _metaFile.DisposeAsync();

            _zipFile.PutNextEntry(Path.GetFileName(_metaFileName));
            WriteFileStream(_metaFileName, new FileInfo(_metaFileName).Length, cancellationToken);

            _zipFile.Close();
            await _zipFile.DisposeAsync();

            _zipFile = null;

            _totalArchiveSize += new FileInfo(_zipFileName).Length;

            // upload
            if (!cancellationToken.IsCancellationRequested)
            {
                await UploadArchiveWithRetries(_zipFileName, cancellationToken);
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

            if (!_savedState.DeleteFiles)
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
