using EcorRouge.Archive.Utility.CloudConnectors;
using EcorRouge.Archive.Utility.Settings;
using log4net;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using System;
using System.Windows;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcorRouge.Archive.Utility.ViewModels
{
    public partial class ExtractWindowViewModel : ObservableObject
    {
        internal static readonly ILog log = LogManager.GetLogger(typeof(ExtractWindowViewModel));

        public const int TAB_SELECT_FILE = 0;
        public const int TAB_VIEW_FILES = 1;
        public const int TAB_SETTINGS = 2;        
        public const int TAB_PROGRESS = 3;
        public const int TAB_FINISH = 4;

        private int _selectedPageIndex = TAB_SELECT_FILE;
        private string _fileName;
        private long _totalFilesInArchive;
        private long _totalFileSizeInArchive;
        private bool _canSelectFile = true;
        private bool _canSelectSettings = false;
        private bool _canSelectViewFiles = false;
        private bool _canSelectProgress = false;
        private bool _canSelectFinish = false;

        public string AppVersionString { get; } = "v." + typeof(ArchiveWindowViewModel).Assembly.GetName().Version;

        public RelayCommand SelectFileCommand { get; set; }
        public RelayCommand SelectSettingsCommand { get; set; }
        public RelayCommand SelectViewFilesCommand { get; set; }
        public RelayCommand SelectProgressCommand { get; set; }
        public RelayCommand SelectFinishCommand { get; set; }

        public RelayCommand ExitCommand { get; private set; }
        public RelayCommand ShowLogsCommand { get; private set; }

        public int SelectedPageIndex
        {
            get => _selectedPageIndex;
            set => SetProperty(ref _selectedPageIndex, value);
        }

        public string FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }

        public long TotalFilesInArchive
        {
            get => _totalFilesInArchive;
            set => SetProperty(ref _totalFilesInArchive, value);
        }

        public long TotalFileSizeInArchive
        {
            get => _totalFileSizeInArchive;
            set => SetProperty(ref _totalFileSizeInArchive, value);
        }

        public bool CanSelectFile
        {
            get => _canSelectFile;
            set => SetProperty(ref _canSelectFile, value);
        }

        public bool CanSelectSettings
        {
            get => _canSelectSettings;
            set => SetProperty(ref _canSelectSettings, value);
        }

        public bool CanSelectViewFiles
        {
            get => _canSelectViewFiles;
            set => SetProperty(ref _canSelectViewFiles, value);
        }

        public bool CanSelectProgress
        {
            get => _canSelectProgress;
            set => SetProperty(ref _canSelectProgress, value);
        }

        public bool CanSelectFinish
        {
            get => _canSelectFinish;
            set => SetProperty(ref _canSelectFinish, value);
        }

        protected virtual void ConfigureRuntimeProperties()
        {
            foreach (var plugin in PluginsManager.Instance.Plugins)
            {
                CloudProviders.Add(plugin.ProviderName);
            }

            _savedState = ExtractSavedState.Load();
        }

        public ExtractWindowViewModel()
        {
            SelectFileCommand = new RelayCommand(() =>
            {
                if (_worker?.IsBusy ?? false)
                {
                    ConfirmInterrupt(() => { SelectedPageIndex = TAB_SELECT_FILE; });
                }
                else
                {
                    SelectedPageIndex = TAB_SELECT_FILE;
                }
            });

            SelectSettingsCommand = new RelayCommand(() =>
            {
                if (_worker?.IsBusy ?? false)
                {
                    ConfirmInterrupt(() => { SelectedPageIndex = TAB_SETTINGS; });
                }
                else
                {
                    SelectedPageIndex = TAB_SETTINGS;
                }
            });

            SelectProgressCommand = new RelayCommand(() =>
            {
                SelectedPageIndex = TAB_PROGRESS;
            });

            SelectFinishCommand = new RelayCommand(() =>
            {
                SelectedPageIndex = TAB_FINISH;
            });

            ShowLogsCommand = new RelayCommand(() =>
            {
                string path = string.Empty;

                try
                {
                    path = PathHelper.GetLogsPath();
                    Process.Start("explorer", path);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open logs directory: {path}. Exception: {ex}");
                }
            });

            ExitCommand = new RelayCommand(() =>
            {
                _fileOpenCts?.Cancel();

                Application.Current.Shutdown(); //TODO: confirmation, cancel file loading, cancel archiving and cleanup
            });

            _savedState = new ExtractSavedState()
            {
                IsEmpty = true
            };

            ConfigureRuntimeProperties();

            InitFilePage();
            //InitSettingsPage();
            //InitProgressPage();
        }

        private void ClearState()
        {
            SavedState.Clear();

            _savedState = new ExtractSavedState()
            {
                IsEmpty = true
            };
        }

        public void CheckSavedState()
        {
        }
    }
}
