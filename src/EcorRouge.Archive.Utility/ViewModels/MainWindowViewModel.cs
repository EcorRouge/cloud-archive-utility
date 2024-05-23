using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EcorRouge.Archive.Utility.CloudConnectors;
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

            SourceCloudConnectors.Add(SettingsFile.DefaultConnectorType);
            foreach (var connectorsFacade in CloudConnectorsManager.Instance.ConnectorsFacades)
            {
                SourceCloudConnectors.Add(connectorsFacade.ConnectorType);
            }

            _savedState = SavedState.Load();
        }

        public MainWindowViewModel()
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

        private void ClearState()
        {
            SavedState.Clear();

            _savedState = new SavedState()
            {
                IsEmpty = true
            };
        }

        public void CheckSavedState()
        {
            if (!_savedState.IsEmpty)
            {
                DisplayYesNoDialog(
                    "Confirm restore",
                    "Previous unfinished archive session was detected. Do you want to recover it and continue where it stopped?",
                    250,
                    () =>
                    {
                        FileName = _savedState.InputFileName;

                        TotalFilesToArchive = _savedState.TotalFilesToArchive;
                        TotalFileSizeToArchive = _savedState.TotalFilesSizeToArchive;
                        DeleteFilesAfterUpload = _savedState.DeleteFiles;
                        MaximumFiles = _savedState.MaximumFiles;
                        MaximumArchiveSizeMb = _savedState.MaximumArchiveSizeMb;

                        SelectedModeIndex = _savedState.SelectedMode;

                        if (SelectedModeIndex == MODE_UPLOAD)
                        {
                            SelectedProviderIndex = CloudProviders.IndexOf(_savedState.PluginType);
                            SetModelValues(PluginProperties, _savedState.GetPluginProperties());
                            OnPropertyChanged(nameof(PluginProperties));


                            SelectedConnectorType = _savedState.ConnectorType;
                            SetModelValues(ConnectorProperties, _savedState.GetConnectorProperties());
                            OnPropertyChanged(nameof(ConnectorProperties));
                        }

                        SelectedPageIndex = TAB_PROGRESS;

                        CanSelectFile = true;
                        CanSelectSettings = false;
                        CanSelectProgress = true;
                        CanSelectFinish = false;

                        if (!String.IsNullOrEmpty(FileName))
                        {
                            try
                            {
                                string[] connectorsMarkers = CloudConnectorsManager.Instance.ConnectorsFacades.SelectMany(c => c.Markers).ToArray();
                                _inputFile = InputFileParser.ScanFile(FileName, connectorsMarkers);

                                if (_inputFile.ConnectorMarker != null)
                                {
                                    SelectConnector(_inputFile.ConnectorMarker);
                                }

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

                        StartArchiving(true);
                    },
                    ClearState, ClearState
                    );
            }
        }

        static void SetModelValues(PropertyModel[] targetPropsModels, Dictionary<string, object> sourceProps)
        {
            foreach (var property in targetPropsModels)
            {
                if (!sourceProps.ContainsKey(property.Name))
                    continue;

                property.Value = sourceProps[property.Name]?.ToString();
            }
        }

        private void SelectConnector(string connectorPrefix)
        {
            string connectorType = CloudConnectorsManager.Instance.ConnectorsFacades.First(c =>
                 c.Markers.Contains(connectorPrefix, StringComparer.OrdinalIgnoreCase)).ConnectorType;

            SelectedConnectorType = connectorType;
        }
    }
}
