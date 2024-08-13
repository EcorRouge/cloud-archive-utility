using EcorRouge.Archive.Utility.Converters;
using EcorRouge.Archive.Utility.Settings;
using EcorRouge.Archive.Utility.Util;
using Microsoft.Toolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcorRouge.Archive.Utility.ViewModels
{
    public partial class ExtractWindowViewModel
    {
        private bool _canCancelProcess = false;
        private ExtractSavedState _savedState;
        private ExtractWorker _worker;

        private string _extractingLabel;
        private string _downloadingLabel;
        private string _totalLabel;
        private string _currentFileLabel;

        private bool _downloadingVisible;

        private double _extractProgress = 0;
        private double _downloadProgress = 0;
        private double _totalProgress = 0;


        public RelayCommand CancelProgressCommand { get; set; }

        public bool CanCancelProcess
        {
            get => _canCancelProcess;
            set => SetProperty(ref _canCancelProcess, value);
        }

        public string ExtractingLabel
        {
            get => _extractingLabel;
            set => SetProperty(ref _extractingLabel, value);
        }

        public string DownloadingLabel
        {
            get => _downloadingLabel;
            set => SetProperty(ref _downloadingLabel, value);
        }

        public string TotalLabel
        {
            get => _totalLabel;
            set => SetProperty(ref _totalLabel, value);
        }

        public string CurrentFileLabel
        {
            get => _currentFileLabel;
            set => SetProperty(ref _currentFileLabel, value);
        }

        public bool DownloadingVisible
        {
            get => _downloadingVisible;
            set => SetProperty(ref _downloadingVisible, value);
        }

        public double ExtractProgress
        {
            get => _extractProgress;
            set => SetProperty(ref _extractProgress, value);
        }

        public double DownloadProgress
        {
            get => _downloadProgress;
            set => SetProperty(ref _downloadProgress, value);
        }

        public double TotalProgress
        {
            get => _totalProgress;
            set => SetProperty(ref _totalProgress, value);
        }

        private void InitProgressPage()
        {
            CancelProgressCommand = new RelayCommand(CancelProgress);
        }

        private void CancelProgress()
        {
            if (_worker?.IsBusy ?? false)
                ConfirmInterrupt(() => { });
        }

        public void ConfirmInterrupt(Action confirmAction)
        {
            DisplayYesNoDialog(
                "Confirm interrupt",
                "Current extracting will be interrupted, do you want to continue?",
                150,
                () =>
                {
                    CanCancelProcess = false;
                    _worker?.Cancel();

                    confirmAction();
                },
                () => { }, () => { }
            );
        }

        public void StartExtracting()
        {
            StartExtracting(false);
        }

        public void StartExtracting(bool saved)
        {
            ExtractingLabel = "Initializing...";
            CanSelectSettings = false;
            CanSelectProgress = true;
            SelectedPageIndex = TAB_PROGRESS;

            DownloadingVisible = false;
            ExtractProgress = 0;
            DownloadProgress = 0;
            TotalProgress = 0;
            
            DownloadingLabel = "Initializing";
            TotalLabel = "Initializing";
            CurrentFileLabel = string.Empty;

            var plugin = PluginsManager.Instance.Plugins[SelectedProviderIndex];
            var pluginProperties = GetProperties(_pluginProperties);
            AddPropertiesToSettings(plugin.ProviderName, pluginProperties);

            if (_savedState.IsEmpty)
            {
                _savedState.SetPluginProperties(pluginProperties);
            }

            _savedState.PluginType = plugin?.ProviderName;
            _savedState.KeypairFilename = _keypairFileName;
            _savedState.SearchExpression = _searchExpression;
            _savedState.InputFilename = _fileName;
            _savedState.IsEncrypted = IsEncrypted;
            _savedState.DestinationFolder = _destinationFolder;

            _savedState.Save();

            _worker = new ExtractWorker(plugin, _savedState, _inputFile);

            _worker.StateChanged += ExtractWorker_StateChanged;
            _worker.Completed += ExtractWorker_Completed;
            _worker.ExtractingProgress += ExtractWorker_ExtractingProgress;
            _worker.DownloadingProgress += ExtractWorker_DownloadingProgress;
            _worker.ExtractingNewFile += ExtractWorker_ExtractingNewFile;

            _worker.Start(SelectedFiles.ToList());
        }

        private void ExtractWorker_DownloadingProgress(object sender, EventArgs e)
        {
            if (_worker.State == ExtractState.DownloadWaiting)
            {
                DownloadingLabel = $"Downloading archive: {_worker.SecondsBeforeRetry} seconds left before next attempt...";
            }
            else
            {
                DownloadProgress = _worker.DownloadProgress;
                DownloadingLabel = $"Downloading archive: {DownloadProgress:N1}% ({FileSizeFormatter.Format(_worker.BytesDownloaded)})";

                FormatTotalLabel();
            }
        }

        private void ExtractWorker_ExtractingProgress(object sender, EventArgs e)
        {
            if(_worker.State == ExtractState.Extracting)
            {
                ExtractProgress = _worker.ExtractProgress;

                ExtractingLabel = $"Extracting archive: {ExtractProgress:N1}% ({FileSizeFormatter.Format(_worker.BytesExtracted)})";
            } 
            else if (_worker.State == ExtractState.Decrypting)
            {
                ExtractProgress = _worker.BytesDecrypted * 100.0 / _worker.ZipFileSize;
                ExtractingLabel = $"Decrypting archive: {ExtractProgress:N1}% ({FileSizeFormatter.Format(_worker.BytesDecrypted)})";
            }

            FormatTotalLabel();
        }

        private void ExtractWorker_Completed(object sender, EventArgs e)
        {
            _worker.StateChanged -= ExtractWorker_StateChanged;
            _worker.Completed -= ExtractWorker_Completed;
            _worker.ExtractingProgress -= ExtractWorker_ExtractingProgress;
            _worker.DownloadingProgress -= ExtractWorker_DownloadingProgress;
            _worker.ExtractingNewFile -= ExtractWorker_ExtractingNewFile;

            if (_worker.State == ExtractState.ErrorStarting)
                return;

            CanSelectSettings = false;
            CanSelectProgress = false;
            CanSelectFinish = true;

            TotalCompletedFilesLabel = $"Total files processed: {_worker.FilesProcessed}, errors: {_worker.FilesFailed}";
            TotalCompletedBytesLabel = $"Total bytes processed: {FileSizeFormatter.Format(_worker.BytesProcessed)}";

            if (_worker.IsCanceled)
            {
                SelectedPageIndex = TAB_SELECT_FILE;
            }
            else
            {
                SelectedPageIndex = TAB_FINISH;
            }
        }

        private void ExtractWorker_StateChanged(object sender, EventArgs e)
        {
            switch (_worker.State)
            {
                case ExtractState.Initializing:
                    DownloadingLabel = "Initializing...";
                    DownloadingVisible = false;
                    break;
                case ExtractState.ErrorStarting:
                    CanSelectSettings = true;
                    CanSelectProgress = false;
                    SelectedPageIndex = TAB_SETTINGS;

                    DisplayOkDialog("Error", "Error testing cloud provider credentials!", () => { });
                    break;
                case ExtractState.Downloading:
                    DownloadProgress = 0;
                    DownloadingLabel = "Downloading...";
                    DownloadingVisible = true;
                    break;
                case ExtractState.Decrypting:
                    DownloadProgress = 0;
                    DownloadingLabel = "Decrypting...";
                    DownloadingVisible = true;
                    break;
                case ExtractState.Extracting:
                    DownloadProgress = 0;
                    DownloadingLabel = "Extracting...";
                    DownloadingVisible = true;
                    break;
                case ExtractState.Completed:
                    break;
            }
        }

        private void ExtractWorker_ExtractingNewFile(ManifestFileEntry fileToExtract)
        {
            CurrentFileLabel = $"{fileToExtract.FileName} ({FileSizeFormatter.Format(fileToExtract.FileSize)})";
        }

        private void FormatTotalLabel()
        {
            TotalProgress = _worker.FilesProcessed * 100.0 / _totalSelectedFiles;

            TotalLabel = $"Total progress: {TotalProgress:N1}%, {FileSizeFormatter.Format(_worker.BytesProcessed)}, {_worker.FilesFailed} errors.";
        }
    }
}
