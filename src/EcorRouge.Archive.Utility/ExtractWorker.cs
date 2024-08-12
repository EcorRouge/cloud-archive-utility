using EcorRouge.Archive.Utility.Plugins;
using EcorRouge.Archive.Utility.Settings;
using EcorRouge.Archive.Utility.Util;
using Ionic.Zip;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Windows.Forms.AxHost;

namespace EcorRouge.Archive.Utility
{
    public enum ExtractState
    {
        Initializing,
        ErrorStarting,
        Downloading,
        DownloadWaiting,
        Decrypting,
        Extracting,
        Completed
    }

    public class ExtractWorker
    {
        internal static readonly ILog log = LogManager.GetLogger(typeof(ExtractWorker));

        private CancellationTokenSource _cts;
        private ExtractSavedState _savedState;

        private PluginBase _plugin;
        private Dictionary<string, object> _pluginProperties;

        private string _destinationFolder;

        private long _filesInArchive = 0;
        private long _filesSizeInArchive = 0;
        private long _filesProcessed = 0;
        private long _bytesProcessed = 0;
        private long _filesFailed = 0;

        private long _totalArchiveSize = 0;
        private long _zipFileSize = 0;
        private long _bytesDownloaded = 0;
        private double _downloadProgress = 0;

        private string _manifestFileName;
        private RSA _rsa;
        private Aes _aesAlg;
        private ICryptoTransform _decryptor;
        private CryptoStream _cryptoStream;
        private string _zipFileName;
        
        private ManifestFile _inputFile;
        private ZipFile _zipFile;

        public event EventHandler Completed;
        public event EventHandler ExtractingProgress;
        public event EventHandler DownloadingProgress;
        public event EventHandler StateChanged;

        public bool IsCanceled { get; private set; }
        public bool IsBusy { get; private set; }
        public List<ManifestFileEntry> SelectedFiles { get; private set; }

        public ExtractState State { get; private set; }
        public int SecondsBeforeRetry { get; set; }


        public ExtractWorker(PluginBase plugin, ExtractSavedState savedState, ManifestFile inputFile)
        {
            this._plugin = plugin;
            this._inputFile = inputFile;
            _savedState = savedState;
            _pluginProperties = _savedState.GetPluginProperties();
            _destinationFolder = _savedState.DestinationFolder

            if (_plugin != null)
            {
                _plugin.OnLogMessage += ExtractorPlugin_OnLogMessage;
                _plugin.OnDownloadProgress += ExtractorPlugin_OnDownloadProgress;
            }
        }

        private void ExtractorPlugin_OnDownloadProgress(long bytesDownloaded, double percentDownloaded)
        {
            _bytesDownloaded = bytesDownloaded;
            _downloadProgress = percentDownloaded;

            DownloadingProgress?.Invoke(this, EventArgs.Empty);
        }

        private void ExtractorPlugin_OnLogMessage(LogLevel level, string message)
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

        public void Start(List<ManifestFileEntry> selectedFiles)
        {
            IsCanceled = false;

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

        private void ChangeState(ExtractState state)
        {
            if (State == state)
                return;

            State = state;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private async Task DownloadArchiveWithRetries(string zipFileName, CancellationToken cancellationToken)
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

                    ChangeState(ExtractState.DownloadWaiting);
                    while (SecondsBeforeRetry > 0 && !cancellationToken.IsCancellationRequested)
                    {
                        SecondsBeforeRetry--;
                        DownloadingProgress?.Invoke(this, EventArgs.Empty);
                        Thread.Sleep(1000);
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                    return;

                ChangeState(ExtractState.Downloading);

                try
                {
                    if (!_plugin.KeepSession)
                    {
                        await _plugin.OpenSessionAsync(_pluginProperties, cancellationToken);
                    }

                    try
                    {
                        await _plugin.DownloadFileAsync(zipFileName, Path.Combine(PathHelper.GetTempPath(), zipFileName), cancellationToken);
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

        private async Task DoWork(CancellationToken cancellationToken)
        {
            IsBusy = true;

            ChangeState(ExtractState.Initializing);

            if (_savedState.IsEncrypted)
            {
                try
                {
                    _rsa = ArchiverWorker.ImportKeypair(_savedState.KeypairFilename);
                }
                catch (Exception ex)
                {
                    log.Error($"Error importing selected keypair {_savedState.KeypairFilename}: {ex.Message}", ex);

                    ChangeState(ExtractState.ErrorStarting);
                    IsBusy = false;
                    return;
                }
            }

            if (_plugin != null)
            {
                try
                {
                    await _plugin.TryConnectAndWriteSmallFile(_pluginProperties, cancellationToken);
                }
                catch (Exception ex)
                {
                    log.Error($"Error testing credentials: {ex.Message}", ex);

                    ChangeState(ExtractState.ErrorStarting);
                    IsBusy = false;
                    return;
                }
            }

            var entries = SelectedFiles.OrderBy(x => x.ZipFileName);

            _zipFileName = null;

            if (_plugin?.KeepSession ?? false)
            {
                await _plugin.OpenSessionAsync(_pluginProperties, cancellationToken);
            }

            foreach (var entry in entries)
            {
                if (_cts.IsCancellationRequested)
                    break;

                if(_zipFile == null || !String.Equals(entry.ZipFileName, _zipFileName))
                {
                    _zipFileName = entry.ZipFileName;

                    log.Debug($"Downloading {entry.ZipFileName}");

                    await DownloadArchiveWithRetries(_zipFileName, cancellationToken);

                    if(entry.ZipFileName.EndsWith(".zip.enc"))
                    {
                        string newZipFileName = Path.GetFileNameWithoutExtension(entry.ZipFileName); //Cut .enc

                        await DecryptArchive(_entry.ZipFileName, newZipFileName, cancellationToken);

                        _zipFile = ZipFile.Read(Path.Combine(PathHelper.GetTempPath(), newZipFileName));
                    } else
                    {
                        _zipFile = ZipFile.Read(Path.Combine(PathHelper.GetTempPath(), zipFileName));
                    }                    
                }

                var zipEntry = _zipFile.Entries.FirstOrDefault(x => x.FileName.Equals(entry.GeneratedFileName);

                if(zipEntry == null) 
                {
                    log.Warn($"Couldn't find entry {entry.GeneratedFileName} for {entry.FileName} in zip: {entry.ZipFileName}");
                } else
                {
                    try
                    {
                        ChangeState(ExtractState.Extracting);
                        zipEntry.Extract(PathHelper.GetTempPath(), ExtractExistingFileAction.OverwriteSilently); //TODO: progress
                    } catch (Exception ex)
                    {
                        log.Error($"Error extracting {entry.GeneratedFileName} from {entry.ZipFileName}", ex);
                    }

                    string tempFileName = Path.Combine(PathHelper.GetTempPath(), entry.GeneratedFileName);
                    string destFileName = Path.Combine(_destinationFolder, entry.FileName);

                    try
                    {
                        File.Move(tempFileName, destFileName);
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Error moving {tempFileName} to {destFileName}: {ex.Message}", ex);
                    }
                }

                _filesProcessed++;

                ExtractingProgress?.Invoke(this, EventArgs.Empty);
            }

            ExtractSavedState.Clear();
        }
    }
}