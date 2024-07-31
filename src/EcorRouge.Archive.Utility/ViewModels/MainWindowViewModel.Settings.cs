using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using EcorRouge.Archive.Utility.CloudConnectors;
using EcorRouge.Archive.Utility.Plugins;
using Microsoft.Toolkit.Mvvm.Input;
using EcorRouge.Archive.Utility.Settings;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.IO;
using System.Text;

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
        private bool _encryptFiles;
        private string _keypairFileName;
        private int _maximumFiles;
        private int _maximumArchiveSizeMb;

        public ObservableCollection<string> CloudProviders { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> SourceCloudConnectors { get; } = new ObservableCollection<string>();

        public RelayCommand StartCommand { get; set; }
        public RelayCommand ChooseKeypairFileCommand { get; set; }
        public RelayCommand GenerateKeypairCommand { get; set; }


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

        public bool EncryptFiles
        {
            get => _encryptFiles;
            set
            {
                SetProperty(ref _encryptFiles, value);
                if(!value)
                {
                    KeypairFileName = null;
                }

                CanStart = CheckCanStart();
            }
        }

        public string KeypairFileName
        {
            get => _keypairFileName;
            set => SetProperty(ref _keypairFileName, value);
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
            ChooseKeypairFileCommand = new RelayCommand(ChooseKeypair);
            GenerateKeypairCommand = new RelayCommand(GenerateKeypair);

            SelectedProviderIndex = SettingsFile.Instance.ProviderIndex;
            SelectedConnectorType = SettingsFile.Instance.ConnectorType;
            DeleteFilesAfterUpload = SettingsFile.Instance.DeleteFilesAfterUpload;
            KeypairFileName = SettingsFile.Instance.KeypairFileName;
            EncryptFiles = SettingsFile.Instance.EncryptFiles;
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
            CanStart = CheckCanStart();
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

            bool encryptPropsOk = !_encryptFiles || !String.IsNullOrWhiteSpace(_keypairFileName);

            bool pluginPropsOk = plugin.VerifyProperties(GetProperties(_pluginProperties));

            if (SelectedConnectorType != null && SelectedConnectorType != SettingsFile.DefaultConnectorType)
            {
                bool connectorProsOk = _connectorProperties.All(p => !string.IsNullOrWhiteSpace(p.Value));
                return connectorProsOk && pluginPropsOk && encryptPropsOk;
            }
            else
            {
                return pluginPropsOk && encryptPropsOk;
            }
        }

        private static void AddPropertiesToSettings(string settingProvider, Dictionary<string, object> props)
        {
            foreach (var value in props)
            {
                SettingsFile.Instance.AddProviderProperty(settingProvider, value.Key, value.Value?.ToString());
            }
        }

        private RSA ImportKeypair(string filename)
        {
            var rsa = RSA.Create();

            string keys;

            using (var reader = new StreamReader(filename))
            {
                keys = reader.ReadToEnd();
            }

            rsa.ImportFromPem(keys.ToCharArray());

            return rsa;
        }

        public void ChooseKeypair()
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "Key files|*.key";

            if(ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _ = ImportKeypair(ofd.FileName);
                    KeypairFileName = ofd.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading keypair: {ex.Message}");
                    log.Error($"Error loading keypair: {ex.Message}", ex);
                }
            }

            CanStart = CheckCanStart();
        }

        public void GenerateKeypair()
        {
            var sfd = new SaveFileDialog();
            sfd.Filter = "Key files|*.key";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var rsa = RSA.Create();

                    using (var file = new StreamWriter(sfd.FileName))
                    {
                        file.WriteLine(rsa.ExportRSAPrivateKeyPem());
                        //file.WriteLine();
                        file.WriteLine(rsa.ExportRSAPublicKeyPem());
                    }

                    KeypairFileName = sfd.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error generating keypair: {ex.Message}");
                    log.Error($"Error generating keypair: {ex.Message}", ex);
                }
            }

            CanStart = CheckCanStart();
        }
    }
}
