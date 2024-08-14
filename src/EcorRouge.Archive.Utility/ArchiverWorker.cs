using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EcorRouge.Archive.Utility.CloudConnectors;
using Ionic.Zip;
using log4net;
using EcorRouge.Archive.Utility.Extensions;
using EcorRouge.Archive.Utility.Plugins;
using EcorRouge.Archive.Utility.Settings;
using EcorRouge.Archive.Utility.Util;
using System.Windows.Forms;
using System.Security.Cryptography;

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
        private const char CSV_SEP = '|';

        private PluginBase _plugin;
        private Dictionary<string, object> _pluginProperties;

        private ConnectorFacade _connectorFacade;

        private SavedState _savedState;

        private CancellationTokenSource _cts;

        private long _filesInArchive = 0;
        private long _filesSizeInArchive = 0;
        private long _filesProcessed = 0;
        private long _bytesProcessed = 0;
        private long _filesFailed = 0;

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
        private RSA _rsa;
        private Aes _aesAlg;
        private ICryptoTransform _encryptor;
        private CryptoStream _cryptoStream;
        private FileStream _outFile;
        private string _zipFileName;
        private InputFile _inputFile;
        private List<string> _archiveFileList;
        private long _deletedFilesCount;

        public event EventHandler Completed;
        public event EventHandler ArchivingProgress;
        public event EventHandler UploadingProgress;
        public event EventHandler DeletingProgress;
        public event EventHandler StateChanged;
        public event Action<InputFileEntry> ArchivingNewFile;

        public bool IsCanceled { get; private set; }
        public bool IsBusy { get; private set; }

        public long FilesInArchive => _filesInArchive;
        public long FilesFailed => _filesFailed;
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

        public string DownloadsDir { get; } = Path.Combine(PathHelper.GetTempPath(), "Downloads");

        public static RSA ImportKeypair(string filename, bool importPublic, bool importPrivate)
        {
            var rsa = RSA.Create();

            var privateKey = new StringBuilder();
            var publicKey = new StringBuilder();

            bool publicSection = false, privateSection = false;

            using (var reader = new StreamReader(filename))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (String.IsNullOrWhiteSpace(line))
                        continue;

                    if (privateSection || line.StartsWith("-----BEGIN RSA PRIVATE"))
                    {
                        privateSection = true;
                        privateKey.AppendLine(line);

                        if (line.StartsWith("-----END RSA"))
                            privateSection = false;
                    }

                    if (publicSection || line.StartsWith("-----BEGIN RSA PUBLIC"))
                    {
                        publicSection = true;
                        publicKey.AppendLine(line);

                        if (line.StartsWith("-----END RSA"))
                            publicSection = false;
                    }
                }
            }

            if (privateKey.Length == 0)
            {
                throw new ArgumentException("Missing private key!");
            }
            if (publicKey.Length == 0)
            {
                throw new ArgumentException("Missing public key!");
            }

            if(importPrivate)
                rsa.ImportFromPem(privateKey.ToString().ToCharArray());

            if(importPublic)
                rsa.ImportFromPem(publicKey.ToString().ToCharArray());

            return rsa;
        }

        public ArchiverWorker(PluginBase plugin, ConnectorFacade connectorFacade, SavedState savedState, InputFile inputFile)
        {
            this._plugin = plugin;
            this._inputFile = inputFile;
            _savedState = savedState;
            _pluginProperties = _savedState.GetPluginProperties();
            _connectorFacade = connectorFacade;

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
            _filesFailed = 0;
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

            if(_savedState.EncryptFiles)
            {
                try
                {
                    _rsa = ImportKeypair(_savedState.KeypairFilename, true, false);
                }
                catch (Exception ex)
                {
                    log.Error($"Error importing selected keypair {_savedState.KeypairFilename}: {ex.Message}", ex);

                    _savedState.EncryptFiles = false;
                    _savedState.KeypairFilename = null;
                    _savedState.Save();
                }
            }

            if (!_savedState.IsEmpty)
            {
                _manifestFileName = _savedState.ManifestFileName;
                _manifestWriter = new StreamWriter(_manifestFileName, true, Encoding.UTF8);
            }
            else
            {
                _manifestFileName = Path.Combine(PathHelper.GetTempPath(true), $"_manifest-{DateTime.Now:yyyy-MM-dd-HH-mm}.csv");

                if (File.Exists(_manifestFileName))
                {
                    File.Delete(_manifestFileName);
                }

                _manifestWriter = new StreamWriter(_manifestFileName, false, Encoding.UTF8);
                await _manifestWriter.WriteLineAsync($"sep={CSV_SEP}");
                await WriteManifestLine("File Name", "File Size", "Created At (UTC)", "Zip File Name", "Generated File Name", "Original Path");
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
                using var parser = InputFileParser.OpenFile(_inputFile, _connectorFacade?.CloudPathSeparator);

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

                if (_connectorFacade != null)
                {
                    var creds = _savedState.GetConnectorProperties().ToDictionary(kv => kv.Key, kv => kv.Value?.ToString());
                    _connectorFacade.Connect(creds);
                    Directory.CreateDirectory(DownloadsDir);
                }

                InputFileEntry entry;
                bool deleteOnly = _plugin == null;

                while ((entry = parser.GetNextEntry()) != null && !cancellationToken.IsCancellationRequested)
                {
                    ArchivingNewFile?.Invoke(entry);

                    if (!deleteOnly && _zipFile == null)
                    {
                        OpenZipFile();
                    }

                    ChangeState(_plugin == null ? ArchiverState.Deleting : ArchiverState.Archiving);

                    try
                    {
                        ArchiveFileProgress = 0;

                        if (deleteOnly)
                        {
                            await DeleteResourceAsync(entry, cancellationToken);
                        }
                        else
                        {
                            await ProcessResourceAsync(entry, cancellationToken);
                        }

                        _filesInArchive++;
                    }
                    catch (Exception ex)
                    {
                        _skippedListFile.WriteLine(entry.RawEntryContent);
                        _filesFailed++;
                        log.Error($"Error processing file: {entry.Path}", ex);
                    }

                    _filesProcessed++;

                    ArchivingProgress?.Invoke(this, EventArgs.Empty);

                    if (!deleteOnly)
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

                if (!deleteOnly)
                {
                    await FlushArchive(cancellationToken);
                    await UploadManifest(cancellationToken);
                }

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

        private async ValueTask ProcessResourceAsync(InputFileEntry entry, CancellationToken cancellationToken)
        {
            string newFileName = Guid.NewGuid() + Path.GetExtension(entry.FileName);

            FileInfo fInfo;
            DateTime? createdAt;

            if (_connectorFacade != null)
            {
                string downloadToPath = Path.Combine(DownloadsDir, newFileName);
                await _connectorFacade.DownloadAsync(entry.Path, downloadToPath, cancellationToken);
                fInfo = new FileInfo(downloadToPath);
                createdAt = entry.CreatedAtUtc;
            }
            else
            {
                fInfo = new FileInfo(entry.Path);
                createdAt = fInfo.CreationTimeUtc;
            }

            if (!fInfo.Exists)
                throw new FileNotFoundException("File not found", fInfo.FullName);

            long fileSize = fInfo.Length;

            await _metaFile.WriteLineAsync($"{newFileName}|{fileSize}|{createdAt?.ToUnixTime().ToString()}|{entry.FileName}");

            await WriteManifestLine(
                entry.FileName,
                fileSize.ToString(),
                createdAt?.ToString(CultureInfo.InvariantCulture),
                Path.GetFileName(_zipFileName),
                newFileName,
                entry.OriginalPath
            );

            _zipFile.PutNextEntry(newFileName);
            WriteFileStream(fInfo.FullName, fileSize, cancellationToken);

            if (_connectorFacade != null)
            {
                fInfo.Delete();
            }

            _archiveFileList.Add(entry.Path);
            _bytesProcessed += fileSize;
        }

        private Task WriteManifestLine(string fileName, string fileSize, string createdAt, string zipFileName, string newFileName, string origPath) =>
            _manifestWriter.WriteLineAsync($"{fileName}{CSV_SEP}{fileSize}{CSV_SEP}{createdAt}{CSV_SEP}{zipFileName}{CSV_SEP}{newFileName}{CSV_SEP}{origPath}");
        
        private async ValueTask DeleteResourceAsync(InputFileEntry entry, CancellationToken cancellationToken)
        {
            long fileSize = entry.FileSize;

            if (_connectorFacade == null)
            {
                fileSize = new FileInfo(entry.Path).Length;
            }

            await DeleteResourceAsync(entry.Path, cancellationToken);
            _bytesProcessed += fileSize;
        }

        private bool ShouldFlushZip()
        {
            if (_savedState.EncryptFiles)
            {
                return _filesInArchive >= _savedState.MaximumFiles || _filesSizeInArchive / (1024 * 1024) >= _savedState.MaximumArchiveSizeMb;
            }
            else
            {
                return _filesInArchive >= _savedState.MaximumFiles || _zipFile.Position / (1024 * 1024) >= _savedState.MaximumArchiveSizeMb;
            }
        }

        private void WriteEncryptedFileHeader()
        {
            var header = Encoding.ASCII.GetBytes("ERAU");
            _outFile.Write(header, 0, header.Length);
            _outFile.Write(BitConverter.GetBytes(1));
            _outFile.Write(BitConverter.GetBytes(_aesAlg.KeySize));

            var iv = _rsa.Encrypt(_aesAlg.IV, RSAEncryptionPadding.Pkcs1);
            _outFile.Write(BitConverter.GetBytes(iv.Length));
            _outFile.Write(iv, 0, iv.Length);

            var key = _rsa.Encrypt(_aesAlg.Key, RSAEncryptionPadding.Pkcs1);
            _outFile.Write(BitConverter.GetBytes(key.Length));
            _outFile.Write(key, 0, key.Length);
        }

        private void OpenZipFile()
        {
            var guid = Guid.NewGuid();

            _archiveFileList = new List<string>();

            _metaFileName = Path.Combine(PathHelper.GetTempPath(true), guid + ".txt");
            _metaFile = new StreamWriter(_metaFileName, false, Encoding.UTF8);

            _zipFileName = Path.Combine(PathHelper.GetTempPath(), guid + ".zip");

            if(_savedState.EncryptFiles)
            {
                _zipFileName += ".enc";
            }

            _savedState.BytesProcessed = BytesProcessed;
            _savedState.FilesProcessed = FilesProcessed;
            _savedState.ArchiveFileName = _zipFileName;
            _savedState.TotalArchivedSize = _totalArchiveSize;
            _savedState.Save();

            if (_savedState.EncryptFiles)
            {
                _aesAlg = Aes.Create();
                _aesAlg.KeySize = 256;
                _aesAlg.GenerateIV();
                _aesAlg.GenerateKey();

                _encryptor = _aesAlg.CreateEncryptor(_aesAlg.Key, _aesAlg.IV);
                _outFile = new FileStream(_zipFileName, FileMode.Create, FileAccess.Write);

                WriteEncryptedFileHeader();

                _cryptoStream = new CryptoStream(_outFile, _encryptor, CryptoStreamMode.Write, true);
                _zipFile = new ZipOutputStream(_cryptoStream, true);
            } else
            {
                _zipFile = new ZipOutputStream(_zipFileName);
            }

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
                if (_savedState.EncryptFiles)
                {
                    _zipFileSize = _filesSizeInArchive;
                }
                else
                {
                    _zipFileSize = _zipFile.Position;
                }

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
                    if (!success & ex is not OperationCanceledException) // let's ignore errors in close session and on cancellation
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

            if(_savedState.EncryptFiles)
            {
                if (_cryptoStream != null)
                {
                    _cryptoStream.Close();
                    _cryptoStream.Dispose();
                    _cryptoStream = null;
                }

                if(_encryptor != null)
                {
                    _encryptor.Dispose();
                    _encryptor = null;
                }

                if(_aesAlg != null)
                {
                    _aesAlg.Dispose();
                    _aesAlg = null;
                }

                if(_outFile != null)
                {
                    _outFile.Close();
                    _outFile.Dispose();
                    _outFile = null;
                }
            }

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

            try
            {
                foreach (var fileName in _archiveFileList)
                {
                    await DeleteResourceAsync(fileName, cancellationToken);
                }
            }
            catch (OperationCanceledException cex)
            {
                log.Info($"Deletion canceled.");
            }

            // delete temporarily downloaded files which were not deleted due to processing errors
            foreach (var file in Directory.GetFiles(DownloadsDir))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    log.Error($"Error deleting temporary downloaded file: {file}", ex);
                }
            }
        }

        private async ValueTask DeleteResourceAsync(string resourcePath, CancellationToken cancellationToken)
        {
            try
            {
                if (_connectorFacade != null)
                {
                    await _connectorFacade.DeleteAsync(resourcePath, cancellationToken);
                }
                else
                {
                    File.Delete(resourcePath);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _filesFailed++;
                log.Error($"Failed to delete {resourcePath}", ex);
            }

            _deletedFilesCount++;

            DeletingProgress?.Invoke(this, EventArgs.Empty);
        }
    }
}
