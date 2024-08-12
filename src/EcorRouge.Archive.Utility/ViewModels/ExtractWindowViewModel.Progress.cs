using EcorRouge.Archive.Utility.Settings;
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

            //_savedState.PluginType = plugin?.ProviderName;
            _savedState.KeypairFilename = _keypairFileName;
            _savedState.SearchExpression = _searchExpression;
            _savedState.InputFilename = _fileName;
            _savedState.DestinationFolder = _destinationFolder;

            _savedState.Save();

            /*_worker = new ExtractWorker(plugin, _savedState, _inputFile);

            _worker.StateChanged += ExtractWorker_StateChanged;
            _worker.Completed += ExtractWorker_Completed;
            _worker.ArchivingProgress += ArchiveWorker_ArchivingProgress;
            _worker.DeletingProgress += ArchiveWorker_DeletingProgress;
            _worker.UploadingProgress += ArchiveWorker_UploadingProgress;
            _worker.ArchivingNewFile += ArchiveWorker_ArchivingNewFile;

            _worker.Start(SelectedFiles);*/
        }


    }
}
