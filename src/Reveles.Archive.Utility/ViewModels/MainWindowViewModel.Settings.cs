using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Toolkit.Mvvm.Input;
using Reveles.Archive.Utility.Plugins;
using Reveles.Archive.Utility.Settings;

namespace Reveles.Archive.Utility.ViewModels
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

            return _properties.ToDictionary(x => x.Name, x => (object)x.Value);
        }

        private bool CheckCanStart()
        {
            if (SelectedProviderIndex < 0 || SelectedProviderIndex >= PluginsManager.Instance.Plugins.Count)
                return false;

            var plugin = PluginsManager.Instance.Plugins[SelectedProviderIndex];

            return plugin.VerifyProperties(GetPropertyValues());
        }

        public void StartArchiving()
        {
            if (SelectedProviderIndex < 0 || SelectedProviderIndex >= PluginsManager.Instance.Plugins.Count)
                return;

            var plugin = PluginsManager.Instance.Plugins[SelectedProviderIndex];

            var values = GetPropertyValues();

            foreach (var value in values)
            {
                SettingsFile.Instance.AddProviderProperty(plugin.ProviderName, value.Key, value.Value?.ToString());
            }
            SettingsFile.Instance.Save();
        }
    }
}
