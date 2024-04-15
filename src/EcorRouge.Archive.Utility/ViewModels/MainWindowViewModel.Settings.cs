using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using EcorRouge.Archive.Utility.CloudConnectors;
using EcorRouge.Archive.Utility.Plugins;
using Microsoft.Toolkit.Mvvm.Input;
using EcorRouge.Archive.Utility.Settings;

namespace EcorRouge.Archive.Utility.ViewModels
{
    public partial class MainWindowViewModel
    {
        private const int MODE_UPLOAD = 0;
        private const int MODE_DELETE = 1;

        private PropertyModel[] _properties = null;
        private int _selectedModeIndex = MODE_UPLOAD;
        private bool _canStart;
        private int _selectedProviderIndex;
        private bool _deleteFilesAfterUpload;
        private int _maximumFiles;
        private int _maximumArchiveSizeMb;

        public ObservableCollection<string> CloudProviders { get; } = new ObservableCollection<string>();

        public RelayCommand StartCommand { get; set; }

        public int SelectedModeIndex
        {
            get => _selectedModeIndex;
            set
            {
                SetProperty(ref _selectedModeIndex, value);

                CanStart = CheckCanStart();
            }
        }

        public bool CanStart
        {
            get => _canStart;
            set => SetProperty(ref _canStart, value);
        }

//        [AlsoNotifyFor("SelectedProviderIndex")]
        public PropertyModel[] Properties => _properties;

        public int SelectedProviderIndex
        {
            get => _selectedProviderIndex;
            set
            {
                SetProperty(ref _selectedProviderIndex, value);

                FillProviderProperties();
            }
        }

        public bool DeleteFilesAfterUpload
        {
            get => _deleteFilesAfterUpload;
            set => SetProperty(ref _deleteFilesAfterUpload, value);
        }

        public int MaximumFiles
        {
            get => _maximumFiles;
            set => SetProperty(ref _maximumFiles, value);
        }

        public int MaximumArchiveSizeMb
        {
            get => _maximumArchiveSizeMb;
            set => SetProperty(ref _maximumArchiveSizeMb, value);
        }

        private void InitSettingsPage()
        {
            StartCommand = new RelayCommand(StartArchiving);

            SelectedProviderIndex = SettingsFile.Instance.ProviderIndex;
            DeleteFilesAfterUpload = SettingsFile.Instance.DeleteFilesAfterUpload;
            MaximumFiles = SettingsFile.Instance.MaximumFiles;
            MaximumArchiveSizeMb = SettingsFile.Instance.MaximumArchiveSizeMb;
        }

        private void FillProviderProperties()
        {
            UnsubscribeProperties();

            if (SelectedProviderIndex < 0 || SelectedProviderIndex >= PluginsManager.Instance.Plugins.Count)
            {
                _properties = new PropertyModel[] { };
            }
            else
            {
                var plugin = PluginsManager.Instance.Plugins[SelectedProviderIndex];

                _properties = plugin.Properties.Select(x => new PropertyModel(x, SettingsFile.Instance.GetProviderProperty(plugin.ProviderName, x.Name))).ToArray();
            }

            SubscribeProperties();

            OnPropertyChanged(nameof(Properties));

            CanStart = CheckCanStart();
        }

        private void UnsubscribeProperties()
        {
            if (_properties == null)
                return;

            foreach (var propertyModel in _properties)
            {
                propertyModel.PropertyChanged -= ProviderProperty_PropertyChanged;
            }
        }

        private void SubscribeProperties()
        {
            if (_properties == null)
                return;

            foreach (var propertyModel in _properties)
            {
                propertyModel.PropertyChanged += ProviderProperty_PropertyChanged;
            }
        }

        private void ProviderProperty_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Properties));

            CanStart = CheckCanStart();
        }

        private Dictionary<string, object> GetPropertyValues()
        {
            if (_properties == null)
                return new Dictionary<string, object>();

            return _properties.ToDictionary(x => x.Name, x => (object)x.Value?.Trim());
        }

        private static Dictionary<string, object> GetProperties(PropertyModel[] propsModels)
        {
            if (propsModels == null)
                return new Dictionary<string, object>();

            return propsModels.ToDictionary(x => x.Name, x => (object)x.Value?.Trim());
        }

        private bool CheckCanStart()
        {
            if (SelectedModeIndex == MODE_DELETE)
                return true;

            if (SelectedProviderIndex < 0 || SelectedProviderIndex >= PluginsManager.Instance.Plugins.Count)
                return false;

            var plugin = PluginsManager.Instance.Plugins[SelectedProviderIndex];

            return plugin.VerifyProperties(GetPropertyValues());
        }

        private static void AddPropertiesToSettings(string settingProvider, Dictionary<string, object> props)
        {
            foreach (var value in props)
            {
                SettingsFile.Instance.AddProviderProperty(settingProvider, value.Key, value.Value?.ToString());
            }
        }
    }
}
