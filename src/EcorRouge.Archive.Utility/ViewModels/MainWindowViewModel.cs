using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EcorRouge.Archive.Utility.Settings;
using EcorRouge.Archive.Utility.Util;
using log4net;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;

namespace EcorRouge.Archive.Utility.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        internal static readonly ILog log = LogManager.GetLogger(typeof(MainWindowViewModel));

        public const int TAB_SELECT_FILE = 0;
        public const int TAB_SETTINGS = 1;
        public const int TAB_PROGRESS = 2;
        public const int TAB_FINISH = 3;

        private int _selectedPageIndex = TAB_SELECT_FILE;
        private string _fileName;
        private long _totalFilesToArchive;
        private long _totalFileSizeToArchive;
        private bool _canSelectFile = true;
        private bool _canSelectSettings = false;
        private bool _canSelectProgress = false;
        private bool _canSelectFinish = false;

        public string AppVersionString { get; } = "v." + typeof(MainWindowViewModel).Assembly.GetName().Version;

        public RelayCommand SelectFileCommand { get; set; }
        public RelayCommand SelectSettingsCommand { get; set; }
        public RelayCommand SelectProgressCommand { get; set; }
        public RelayCommand SelectFinishCommand { get; set; }
        
        public RelayCommand ExitCommand { get; set; }

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

        public long TotalFilesToArchive
        {
            get => _totalFilesToArchive;
            set => SetProperty(ref _totalFilesToArchive, value);
        }

        public long TotalFileSizeToArchive
        {
            get => _totalFileSizeToArchive;
            set => SetProperty(ref _totalFileSizeToArchive, value);
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

            _savedState = SavedState.Load();
        }

        public MainWindowViewModel()
        {
            SelectFileCommand = new RelayCommand(() =>
            {
                if (_worker?.IsBusy ?? false)
                {
                    if (!ConfirmInterrupt())
                    {
                        return;
                    }
                }
                SelectedPageIndex = TAB_SELECT_FILE;
            });

            SelectSettingsCommand = new RelayCommand(() =>
            {
                if (_worker?.IsBusy ?? false)
                {
                    if (!ConfirmInterrupt())
                    {
                        return;
                    }
                }

                SelectedPageIndex = TAB_SETTINGS;
            });

            SelectProgressCommand = new RelayCommand(() =>
            {
                SelectedPageIndex = TAB_PROGRESS;
            });

            SelectFinishCommand = new RelayCommand(() =>
            {
                SelectedPageIndex = TAB_FINISH;
            });

            ExitCommand = new RelayCommand(() =>
            {
                _fileOpenCts?.Cancel();

                Application.Current.Shutdown(); //TODO: confirmation, cancel file loading, cancel archiving and cleanup
            });

            _savedState = new SavedState()
            {
                IsEmpty = true
            };

            ConfigureRuntimeProperties();

            InitFilePage();
            InitSettingsPage();
            InitProgressPage();
        }

        public void CheckSavedState(Window owner)
        {
            if (!_savedState.IsEmpty)
            {
                if (MessageBox.Show(owner,
                    "Previous unfinished archive session was detected. Do you want to recover it and continue where it stopped?",
                    "Confirm continue", MessageBoxButton.YesNoCancel) != MessageBoxResult.Yes)
                {
                    SavedState.Clear();

                    _savedState = new SavedState()
                    {
                        IsEmpty = true
                    };
                }
                else
                {
                    FileName = _savedState.InputFileName;

                    TotalFilesToArchive = _savedState.TotalFilesToArchive;
                    TotalFileSizeToArchive = _savedState.TotalFilesSizeToArchive;
                    DeleteFilesAfterUpload = _savedState.DeleteFiles;
                    MaximumFiles = _savedState.MaximumFiles;
                    MaximumArchiveSizeMb = _savedState.MaximumArchiveSizeMb;

                    SelectedProviderIndex = CloudProviders.IndexOf(_savedState.PluginType);
                    var providerProperties = _savedState.GetPluginProperties();
                    foreach (var property in Properties)
                    {
                        if(!providerProperties.ContainsKey(property.Name))
                            continue;

                        property.Value = providerProperties[property.Name]?.ToString();
                    }
                    OnPropertyChanged(nameof(Properties));

                    SelectedPageIndex = TAB_PROGRESS;

                    CanSelectFile = true;
                    CanSelectSettings = false;
                    CanSelectProgress = true;
                    CanSelectFinish = false;

                    if (!String.IsNullOrEmpty(FileName))
                    {
                        try
                        {
                            _inputFile = InputFileParser.ScanFile(FileName);

                            TotalFilesToArchive = _inputFile.TotalFiles;
                            TotalFileSizeToArchive = _inputFile.TotalFilesSize;
                        }
                        catch (Exception ex)
                        {
                            log.Error($"Error parsing {FileName}", ex);

                            FileName = null;
                            CanSelectProgress = false;

                            return;
                        }
                    }

                    StartArchiving();
                }
            }
        }
    }
}
