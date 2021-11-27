using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Toolkit.Mvvm.Input;
using EcorRouge.Archive.Utility.Settings;

namespace EcorRouge.Archive.Utility.ViewModels
{
    public partial class MainWindowViewModel
    {
        private PropertyModel[] _properties = null;
        private bool _canStart;
        private int _selectedProviderIndex;
        private bool _deleteFilesAfterUpload;
        private int _maximumFiles;
        private int _maximumArchiveSizeMb;

        public ObservableCollection<string> CloudProviders { get; } = new ObservableCollection<string>();

        public RelayCommand StartCommand { get; set; }

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

        private bool CheckCanStart()
        {
            if (SelectedProviderIndex < 0 || SelectedProviderIndex >= PluginsManager.Instance.Plugins.Count)
                return false;

            var plugin = PluginsManager.Instance.Plugins[SelectedProviderIndex];

            return plugin.VerifyProperties(GetPropertyValues());
        }
    }
}
