using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Ionic.Zip;
using Microsoft.Toolkit.Mvvm.Input;
using Reveles.Archive.Utility.Converters;
using Reveles.Archive.Utility.Settings;

namespace Reveles.Archive.Utility.ViewModels
{
    public partial class MainWindowViewModel
    {
        private CancellationTokenSource _cts;
        private bool _isBusy;

        private long _filesInArchive = 0;
        private long _filesSizeInArchive = 0;
        private long _filesProcessed = 0;
        private long _bytesProcessed = 0;

        private ZipOutputStream _zipFile;
        private string _zipFileName;

        private bool _canCancelProcess = false;
        private string _archivingLabel;
        private string _uploadingLabel;
        private string _deletingLabel;
        private string _totalLabel;

        private bool _uploadingVisible;
        private bool _deletingVisible;

        private double _archiveProgress = 0;
        private double _uploadProgress = 0;
        private double _deleteProgress = 0;
        private double _totalProgress = 0;

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
            if(_isBusy)
                ConfirmInterrupt();
        }

        public bool ConfirmInterrupt()
        {
            var result = MessageBox.Show("Current archiving will be interrupted, do you want to continue?",
                "Confirm interrupt", MessageBoxButton.YesNoCancel) == MessageBoxResult.Yes;

            if (result)
            {
                CanCancelProcess = false;
                _cts?.Cancel();
            }

            return result;
        }

        public void StartArchiving()
        {
            if (SelectedProviderIndex < 0 || SelectedProviderIndex >= PluginsManager.Instance.Plugins.Count)
                return;

            CanCancelProcess = true;

            var plugin = PluginsManager.Instance.Plugins[SelectedProviderIndex];

            var values = GetPropertyValues();

            foreach (var value in values)
            {
                SettingsFile.Instance.AddProviderProperty(plugin.ProviderName, value.Key, value.Value?.ToString());
            }
            SettingsFile.Instance.Save();

            var size = PathHelper.GetFreeTempDriveSize();

            if (size < MaximumArchiveSizeMb * 1024 * 1024)
            {
                var sizeStr = FileSizeFormatter.Format(size);

                if (MessageBox.Show(
                    $"There's no enough space on current drive to fit archives.\nSpace remaining: {sizeStr}. Archive size configured: {MaximumArchiveSizeMb} Mb. Are you sure want to continue?",
                    "Low space warning",
                    MessageBoxButton.YesNoCancel
                    ) != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            ArchivingLabel = "Initializing...";

            _filesInArchive = 0;
            _filesSizeInArchive = 0;
            _filesProcessed = 0;
            _bytesProcessed = 0;

            CanSelectSettings = false;
            CanSelectProgress = true;
            SelectedPageIndex = TAB_PROGRESS;

            UploadingVisible = false;
            DeletingVisible = false;
            ArchiveProgress = 0;
            UploadProgress = 0;
            DeleteProgress = 0;
            TotalProgress = 0;

            ArchivingLabel = "Initializing";
            UploadingLabel = "Initializing";
            DeletingLabel = "Initializing";
            TotalLabel = "Initializing";

            _cts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                await DoWork(_cts.Token);
            }).ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    log.Error("Error running archive task", t.Exception);
                }

                CanSelectSettings = false;
                CanSelectProgress = false;
                CanSelectFinish = true;

                SelectedPageIndex = TAB_FINISH;
            });
        }

        private void FormatArchiveLabel()
        {
            double countProgress = _filesInArchive * 100.0 / _maximumFiles;
            double sizeProgress = (_filesSizeInArchive / (1024.0 * 1024.0)) * 100.0 / _maximumArchiveSizeMb;

            ArchiveProgress = Math.Max(countProgress, sizeProgress);

            TotalLabel = $"Archiving: {ArchiveProgress:N1}%, {_filesInArchive} files added, {FileSizeFormatter.Format(_zipFile.Position)} ({FileSizeFormatter.Format(_filesSizeInArchive)})";
        }

        private void FormatTotalLabel()
        {
            TotalProgress = _filesProcessed * 100.0 / _totalFilesToArchive;

            TotalLabel = $"Total progress: {TotalProgress:N1}%, {FileSizeFormatter.Format(_bytesProcessed)}";
        }

        private async Task ProcessFileAsync(string fileName, CancellationToken cancellationToken)
        {
            //_zipFile.PutNextEntry(); //TODO: OpenZipFile();

        }

        private bool ShouldFlushZip()
        {
            return _filesInArchive > _maximumFiles || _zipFile.Position / (1024 * 1024) > _maximumArchiveSizeMb;
        }

        private void OpenZipFile()
        {
            _zipFileName = Path.Combine(PathHelper.GetTempPath(), Guid.NewGuid() + ".zip");
            _zipFile = new ZipOutputStream(_zipFileName);
            _zipFile.EnableZip64 = Zip64Option.Always;

            _filesInArchive = 0;
            _filesSizeInArchive = 0;

            FormatArchiveLabel();
        }

        private async Task FlushArchive(CancellationToken cancellationToken)
        {
            //TODO: put meta

            _zipFile.Close();
            await _zipFile.DisposeAsync();

            // upload
            if (!cancellationToken.IsCancellationRequested)
            {
                UploadingVisible = true;


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

            UploadingVisible = false;
            DeletingVisible = false;
        }

        private async Task DoWork(CancellationToken cancellationToken)
        {
            _isBusy = true;

            try
            {
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

                    try
                    {
                        await ProcessFileAsync(parts[0], cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        log.Error("Error processing file", ex);
                    }

                    _filesProcessed++;
                    _filesInArchive++;

                    FormatArchiveLabel();
                    FormatTotalLabel();

                    if (ShouldFlushZip())
                    {
                        await FlushArchive(cancellationToken);

                        OpenZipFile();
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
                _isBusy = false;
            }
        }
    }
}
