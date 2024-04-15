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

        private PropertyModel[] _pluginProperties = null;
        private PropertyModel[] _connectorProperties = null;
        private int _selectedModeIndex = MODE_UPLOAD;
        private bool _canStart;
        private string _selectedConnectorType;
        private int _selectedProviderIndex;
        private bool _deleteFilesAfterUpload;
        private int _maximumFiles;
        private int _maximumArchiveSizeMb;

        public ObservableCollection<string> CloudProviders { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> SourceCloudConnectors { get; } = new ObservableCollection<string>();

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
        public PropertyModel[] PluginProperties => _pluginProperties;

        public PropertyModel[] ConnectorProperties => _connectorProperties;

        public int SelectedProviderIndex
        {
            get => _selectedProviderIndex;
            set
            {
                SetProperty(ref _selectedProviderIndex, value);

                FillProviderProperties();
            }
        }

        public string SelectedConnectorType
        {
            get => _selectedConnectorType;
            set
            {
                SetProperty(ref _selectedConnectorType, value);

                FillConnectorProperties();
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
            SelectedConnectorType = SettingsFile.Instance.ConnectorType;
            DeleteFilesAfterUpload = SettingsFile.Instance.DeleteFilesAfterUpload;
            MaximumFiles = SettingsFile.Instance.MaximumFiles;
            MaximumArchiveSizeMb = SettingsFile.Instance.MaximumArchiveSizeMb;
        }

        private void FillProviderProperties()
        {
            UnsubscribeProperties(_pluginProperties, ProviderProperty_PropertyChanged);

            PluginBase plugin = PluginsManager.Instance.GetPlugin(SelectedProviderIndex);

            _pluginProperties = plugin != null ? GetPropModels(plugin.ProviderName, plugin.Properties).ToArray() : Array.Empty<PropertyModel>();

            SubscribeProperties(_pluginProperties, ProviderProperty_PropertyChanged);

            OnPropertyChanged(nameof(PluginProperties));

            CanStart = CheckCanStart();
        }

        private void FillConnectorProperties()
        {
            UnsubscribeProperties(_connectorProperties, ConnectorProperty_PropertyChanged);

            ConnectorFacade connectorFacade = CloudConnectorsManager.Instance.GetConnectorFacade(SelectedConnectorType);

            if (connectorFacade != null)
            {
                var connProps = CloudConnectorsManager.Instance.GetCredentialsNames(connectorFacade);
                _connectorProperties = GetPropModels(connectorFacade.CredsType, connProps).ToArray();
            }
            else
            {
                _connectorProperties = Array.Empty<PropertyModel>();;
            }

            SubscribeProperties(_connectorProperties, ConnectorProperty_PropertyChanged);

            OnPropertyChanged(nameof(ConnectorProperties));

            CanStart = CheckCanStart();
        }

        IEnumerable<PropertyModel> GetPropModels(string settingProviderName, CloudProviderProperty[] props)
            => props.Select(prop => new PropertyModel(prop, SettingsFile.Instance.GetProviderProperty(settingProviderName, prop.Name)));


        private void UnsubscribeProperties(PropertyModel[] propModels, PropertyChangedEventHandler handler)
        {
            if (propModels == null)
                return;

            foreach (var propertyModel in propModels)
            {
                propertyModel.PropertyChanged -= handler;
            }
        }

        private void SubscribeProperties(PropertyModel[] propModels, PropertyChangedEventHandler handler)
        {
            if (propModels == null)
                return;

            foreach (var propertyModel in propModels)
            {
                propertyModel.PropertyChanged += handler;
            }
        }

        private void ProviderProperty_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(PluginProperties));

            CanStart = CheckCanStart();
        }

        private void ConnectorProperty_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ConnectorProperties));
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

            var plugin = PluginsManager.Instance.GetPlugin(SelectedProviderIndex);
            if (plugin == null) return false;

            bool pluginPropsOk = plugin.VerifyProperties(GetProperties(_pluginProperties));

            if (SelectedConnectorType != null && SelectedConnectorType != SettingsFile.DefaultConnectorType)
            {
                bool connectorProsOk = _connectorProperties.All(p => !string.IsNullOrWhiteSpace(p.Value));
                return connectorProsOk && pluginPropsOk;
            }
            else
            {
                return pluginPropsOk;
            }
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
