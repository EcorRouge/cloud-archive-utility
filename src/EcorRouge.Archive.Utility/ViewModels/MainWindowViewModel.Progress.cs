using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using EcorRouge.Archive.Utility.CloudConnectors;
using Ionic.Zip;
using log4net.Core;
using Microsoft.Toolkit.Mvvm.Input;
using EcorRouge.Archive.Utility.Converters;
using EcorRouge.Archive.Utility.Extensions;
using EcorRouge.Archive.Utility.Plugins;
using EcorRouge.Archive.Utility.Settings;
using EcorRouge.Archive.Utility.Util;

namespace EcorRouge.Archive.Utility.ViewModels
{
    public partial class MainWindowViewModel
    {
        private bool _canCancelProcess = false;
        private string _archivingLabel;
        private string _uploadingLabel;
        private string _deletingLabel;
        private string _totalLabel;
        private string _currentFileLabel;

        private bool _uploadingVisible;
        private bool _deletingVisible;

        private double _archiveProgress = 0;
        private double _archiveFileProgress = 0;
        private double _uploadProgress = 0;
        private double _deleteProgress = 0;
        private double _totalProgress = 0;

        private SavedState _savedState;

        private ArchiverWorker _worker;

        public RelayCommand CancelProgressCommand { get; set; }

        public bool CanCancelProcess
        {
            get => _canCancelProcess;
            set => SetProperty(ref _canCancelProcess, value);
        }

        public string ArchivingLabel
        {
            get => _archivingLabel;
            set => SetProperty(ref _archivingLabel, value);
        }

        public string UploadingLabel
        {
            get => _uploadingLabel;
            set => SetProperty(ref _uploadingLabel, value);
        }

        public string DeletingLabel
        {
            get => _deletingLabel;
            set => SetProperty(ref _deletingLabel, value);
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

        public bool UploadingVisible
        {
            get => _uploadingVisible;
            set => SetProperty(ref _uploadingVisible, value);
        }

        public bool DeletingVisible
        {
            get => _deletingVisible;
            set => SetProperty(ref _deletingVisible, value);
        }

        public double ArchiveProgress
        {
            get => _archiveProgress;
            set => SetProperty(ref _archiveProgress, value);
        }
        public double ArchiveFileProgress
        {
            get => _archiveFileProgress;
            set => SetProperty(ref _archiveFileProgress, value);
        }
        public double UploadProgress
        {
            get => _uploadProgress;
            set => SetProperty(ref _uploadProgress, value);
        }
        public double DeleteProgress
        {
            get => _deleteProgress;
            set => SetProperty(ref _deleteProgress, value);
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
            if(_worker?.IsBusy ?? false)
                ConfirmInterrupt(() => { });
        }

        public void ConfirmInterrupt(Action confirmAction)
        {
            DisplayYesNoDialog(
                "Confirm interrupt",
                "Current archiving will be interrupted, do you want to continue?",
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

        public void StartArchiving()
        {
            StartArchiving(false);
        }

        public void StartArchiving(bool saved)
        {
            if (SelectedModeIndex == MODE_DELETE && !saved)
            {
                var sizeStr = FileSizeFormatter.Format(TotalFileSizeToArchive);

                DisplayYesNoDialog(
                    "Delete warning",
                    $"Your are going to remove {TotalFilesToArchive} files, {sizeStr} of data. This operation cannot be undone. Are you sure you want to continue?",
                    250,
                    () => { StartArchiving(true); },
                    () => { },
                    () => { }
                    );

                return;
            }

            if (SelectedModeIndex == MODE_UPLOAD && (SelectedProviderIndex < 0 || SelectedProviderIndex >= PluginsManager.Instance.Plugins.Count))
                return;

            CanCancelProcess = true;

            PluginBase plugin = null;
            ConnectorFacade sourceConnector = CloudConnectorsManager.Instance.GetConnectorFacade(SelectedConnectorType);
            if (sourceConnector != null)
            {
                var connectorProperties = GetProperties(_connectorProperties);
                AddPropertiesToSettings(sourceConnector.CredsType, connectorProperties);

                if (_savedState.IsEmpty)
                {
                    _savedState.SetConnectorProperties(connectorProperties);
                }

                SettingsFile.Instance.Save();
            }

            if (SelectedModeIndex == MODE_UPLOAD)
            {
                plugin = PluginsManager.Instance.Plugins[SelectedProviderIndex];
                var pluginProperties = GetProperties(_pluginProperties);
                AddPropertiesToSettings(plugin.ProviderName, pluginProperties);

                if (_savedState.IsEmpty)
                {
                    _savedState.SetPluginProperties(pluginProperties);
                }

                SettingsFile.Instance.Save();

                var size = PathHelper.GetFreeTempDriveSize();

                if (size < MaximumArchiveSizeMb * 1024 * 1024)
                {
                    var sizeStr = FileSizeFormatter.Format(size);

                    DisplayYesNoDialog(
                        "Low space warning",
                        $"There's not enough space on current drive to fit archives.\nSpace remaining: {sizeStr}. Archive size configured: {MaximumArchiveSizeMb} Mb. Are you sure want to continue?",
                        250,
                        () =>
                        {
                            StartArchivingInternal(plugin, sourceConnector);
                        },
                        () => { },
                        () => { }
                        );

                    return;
                }
            }

            StartArchivingInternal(plugin, sourceConnector);
        }

        private void StartArchivingInternal(PluginBase plugin, ConnectorFacade sourceConnector)
        {
            ArchivingLabel = "Initializing...";
            CanSelectSettings = false;
            CanSelectProgress = true;
            SelectedPageIndex = TAB_PROGRESS;

            UploadingVisible = false;
            DeletingVisible = false;
            ArchiveProgress = 0;
            ArchiveFileProgress = 0;
            UploadProgress = 0;
            DeleteProgress = 0;
            TotalProgress = 0;

            ArchivingLabel = "Initializing";
            UploadingLabel = "Initializing";
            DeletingLabel = "Initializing";
            TotalLabel = "Initializing";
            CurrentFileLabel = string.Empty;

            _savedState.SelectedMode = _selectedModeIndex;
            _savedState.PluginType = plugin?.ProviderName;
            _savedState.ConnectorType = sourceConnector?.ConnectorType;
            _savedState.DeleteFiles = DeleteFilesAfterUpload;
            _savedState.EncryptFiles = EncryptFiles;
            _savedState.KeypairFilename = KeypairFileName;
            _savedState.InputFileName = _fileName;
            _savedState.TotalFilesToArchive = _totalFilesToArchive;
            _savedState.TotalFilesSizeToArchive = _totalFileSizeToArchive;
            _savedState.MaximumFiles = _maximumFiles;
            _savedState.MaximumArchiveSizeMb = _maximumArchiveSizeMb;
            _savedState.Save();

            _worker = new ArchiverWorker(plugin, sourceConnector, _savedState, _inputFile);
            _worker.StateChanged += ArchiverWorker_StateChanged;
            _worker.Completed += ArchiveWorker_Completed;
            _worker.ArchivingProgress += ArchiveWorker_ArchivingProgress;
            _worker.DeletingProgress += ArchiveWorker_DeletingProgress;
            _worker.UploadingProgress += ArchiveWorker_UploadingProgress;
            _worker.ArchivingNewFile += ArchiveWorker_ArchivingNewFile;
            _worker.Start();
        }

        private void ArchiverWorker_StateChanged(object? sender, EventArgs e)
        {
            switch (_worker.State)
            {
                case ArchiverState.Initializing:
                    break;
                case ArchiverState.ErrorStarting:
                    CanSelectSettings = true;
                    CanSelectProgress = false;
                    SelectedPageIndex = TAB_SETTINGS;

                    DisplayOkDialog("Error", "Error testing cloud provider credentials!", () => { });
                    break;
                case ArchiverState.Archiving:
                    UploadingVisible = false;
                    DeletingVisible = false;
                    break;
                case ArchiverState.Uploading:
                    UploadProgress = 0;
                    UploadingLabel = "Uploading...";
                    UploadingVisible = true;
                    break;
                case ArchiverState.Deleting:
                    DeleteProgress = 0;
                    DeletingLabel = "Deleting...";
                    DeletingVisible = true;
                    break;
                case ArchiverState.Completed:
                    break;
            }
        }

        private void ArchiveWorker_UploadingProgress(object? sender, EventArgs e)
        {
            if (_worker.State == ArchiverState.UploadWaiting)
            {
                UploadingLabel = $"Uploading archive: {_worker.SecondsBeforeRetry} seconds left before next attempt...";
            }
            else
            {
                UploadProgress = _worker.UploadProgress;
                UploadingLabel = $"Uploading archive: {UploadProgress:N1}% ({FileSizeFormatter.Format(_worker.BytesUploaded)})";

                FormatTotalLabel();
            }
        }

        private void ArchiveWorker_DeletingProgress(object? sender, EventArgs e)
        {
            DeleteProgress = _worker.DeletedFilesCount * 100.0 / _worker.FilesInArchive;
            DeletingLabel = $"Deleting files: {_worker.DeletedFilesCount} of {_worker.FilesInArchive}";

            FormatTotalLabel();
        }

        private void ArchiveWorker_ArchivingProgress(object? sender, EventArgs e)
        {
            if (_selectedModeIndex == MODE_DELETE)
            {
                ArchiveProgress = _worker.FilesProcessed * 100.0 / TotalFilesToArchive;
                ArchiveFileProgress = 0;

                ArchivingLabel = $"Processing: {ArchiveProgress:N1}%, {_worker.FilesInArchive} files";
            }
            else
            {
                double countProgress = _worker.FilesInArchive * 100.0 / _maximumFiles;
                double sizeProgress = (_worker.FilesSizeInArchive / (1024.0 * 1024.0)) * 100.0 / _maximumArchiveSizeMb;

                ArchiveProgress = Math.Max(countProgress, sizeProgress);
                ArchiveFileProgress = _worker.ArchiveFileProgress;

                ArchivingLabel = $"Archiving: {ArchiveProgress:N1}%, {_worker.FilesInArchive} files added, {FileSizeFormatter.Format(_worker.FilesSizeInArchive)} (archived: {FileSizeFormatter.Format(_worker.ArchiveSize)})";
            }

            FormatTotalLabel();
        }

        private void ArchiveWorker_ArchivingNewFile(InputFileEntry fileToBeArchived)
        {
            CurrentFileLabel = $"{fileToBeArchived.FileName} ({FileSizeFormatter.Format(fileToBeArchived.FileSize)})";
        }

        private void ArchiveWorker_Completed(object? sender, EventArgs e)
        {
            _worker.StateChanged -= ArchiverWorker_StateChanged;
            _worker.Completed -= ArchiveWorker_Completed;
            _worker.ArchivingProgress -= ArchiveWorker_ArchivingProgress;
            _worker.DeletingProgress -= ArchiveWorker_DeletingProgress;
            _worker.UploadingProgress -= ArchiveWorker_UploadingProgress;
            _worker.ArchivingNewFile -= ArchiveWorker_ArchivingNewFile;

            if (_worker.State == ArchiverState.ErrorStarting)
                return;

            CanSelectSettings = false;
            CanSelectProgress = false;
            CanSelectFinish = true;

            TotalCompletedFilesLabel = $"Total files processed: {_worker.FilesProcessed}, errors: {_worker.FilesFailed}";
            TotalCompletedBytesLabel = $"Total bytes processed: {FileSizeFormatter.Format(_worker.BytesProcessed)} (archived: {FileSizeFormatter.Format(_worker.TotalArchiveSize + _worker.ArchiveSize)} )";

            if (_worker.IsCanceled)
            {
                SelectedPageIndex = TAB_SELECT_FILE;
            }
            else
            {
                SelectedPageIndex = TAB_FINISH;
            }
        }

        private void FormatTotalLabel()
        {
            TotalProgress = _worker.FilesProcessed * 100.0 / _totalFilesToArchive;

            TotalLabel = $"Total progress: {TotalProgress:N1}%, {FileSizeFormatter.Format(_worker.BytesProcessed)}, {_worker.FilesFailed} errors.";
        }
    }
}
