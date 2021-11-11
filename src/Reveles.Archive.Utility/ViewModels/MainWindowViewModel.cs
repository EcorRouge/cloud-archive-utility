using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.Input;
using PropertyChanged;

namespace Reveles.Archive.Utility.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public partial class MainWindowViewModel
    {
        public const int TAB_SELECT_FILE = 0;
        public const int TAB_SETTINGS = 1;
        public const int TAB_PROGRESS = 2;
        public const int TAB_FINISH = 3;

        public RelayCommand SelectFileCommand { get; set; }
        public RelayCommand SelectSettingsCommand { get; set; }
        public RelayCommand SelectProgressCommand { get; set; }
        public RelayCommand SelectFinishCommand { get; set; }

        [DependsOn("CanSelectFile", new [] { "CanBrowseFile" })]
        public RelayCommand BrowseFileCommand { get; set; }
        public RelayCommand ExitCommand { get; set; }

        public int SelectedPageIndex { get; set; } = TAB_SELECT_FILE;

        public string FileName { get; set; }

        public long TotalFilesToArchive { get; set; }
        public long TotalFileSizeToArchive { get; set; }

        public bool CanSelectFile { get; set; } = true;
        public bool CanSelectSettings { get; set; } = false;
        public bool CanSelectProgress { get; set; } = false;
        public bool CanSelectFinish { get; set; } = false;

        protected virtual void ConfigureRuntimeProperties()
        {
            foreach (var plugin in PluginsManager.Instance.Plugins)
            {
                CloudProviders.Add(plugin.ProviderName);
            }
        }

        public MainWindowViewModel()
        {
            SelectFileCommand = new RelayCommand(() =>
            {
                SelectedPageIndex = TAB_SELECT_FILE;
            });

            SelectSettingsCommand = new RelayCommand(() =>
            {
                SelectedPageIndex = TAB_SETTINGS;
            });

            SelectProgressCommand = new RelayCommand(() =>
            {
                SelectedPageIndex = TAB_PROGRESS;
            }, () => CanSelectProgress);

            SelectFinishCommand = new RelayCommand(() =>
            {
                SelectedPageIndex = TAB_FINISH;
            }, () => CanSelectFinish);

            BrowseFileCommand = new RelayCommand(ChooseFile, () => CanSelectFile && CanBrowseFile);

            ExitCommand = new RelayCommand(() =>
            {
                _fileOpenCts.Cancel();

                Application.Current.Shutdown(); //TODO: confirmation, cancel file loading, cancel archiving and cleanup
            });

            ConfigureRuntimeProperties();
        }
    }
}
