using EcorRouge.Archive.Utility.Plugins;
using EcorRouge.Archive.Utility.Settings;
using Microsoft.Toolkit.Mvvm.Input;
using Newtonsoft.Json.Linq;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EcorRouge.Archive.Utility.ViewModels
{
    public partial class ExtractWindowViewModel
    {
        private PropertyModel[] _pluginProperties = null;
        private bool _canStart;
        private int _selectedProviderIndex;
        private string _keypairFileName;
        private bool _isEncrypted;
        private string _destinationFolder;

        public ObservableCollection<string> CloudProviders { get; } = new ObservableCollection<string>();

        public RelayCommand StartCommand { get; set; }
        public RelayCommand ChooseKeypairFileCommand { get; set; }
        public RelayCommand ChooseDestinationFolderCommand { get; set; }

        public bool CanStart
        {
            get => _canStart;
            set => SetProperty(ref _canStart, value);
        }

        public bool IsEncrypted
        {
            get => _isEncrypted;
            set => SetProperty(ref _isEncrypted, value);
        }

        public PropertyModel[] PluginProperties => _pluginProperties;

        public int SelectedProviderIndex
        {
            get => _selectedProviderIndex;
            set
            {
                SetProperty(ref _selectedProviderIndex, value);

                FillProviderProperties();
            }
        }

        public string DestinationFolder
        {
            get => _destinationFolder;
            set => SetProperty(ref _destinationFolder, value);
        }

        public string KeypairFileName
        {
            get => _keypairFileName;
            set => SetProperty(ref _keypairFileName, value);
        }

        private void InitSettingsPage()
        {
            StartCommand = new RelayCommand(StartExtracting);

            ChooseKeypairFileCommand = new RelayCommand(ChooseKeypair);
            ChooseDestinationFolderCommand = new RelayCommand(ChooseDestinationFolder);

            SelectedProviderIndex = SettingsFile.Instance.ProviderIndex;
            KeypairFileName = SettingsFile.Instance.KeypairFileName;
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

        private static Dictionary<string, object> GetProperties(PropertyModel[] propsModels)
        {
            if (propsModels == null)
                return new Dictionary<string, object>();

            return propsModels.ToDictionary(x => x.Name, x => (object)x.Value?.Trim());
        }

        private bool CheckCanStart()
        {
            var plugin = PluginsManager.Instance.GetPlugin(SelectedProviderIndex);
            if (plugin == null) return false;

            bool encryptPropsOk = !_isEncrypted || !String.IsNullOrWhiteSpace(_keypairFileName);

            bool pluginPropsOk = plugin.VerifyProperties(GetProperties(_pluginProperties));

            return pluginPropsOk && encryptPropsOk && !String.IsNullOrWhiteSpace(_destinationFolder);
        }

        private static void AddPropertiesToSettings(string settingProvider, Dictionary<string, object> props)
        {
            foreach (var value in props)
            {
                SettingsFile.Instance.AddProviderProperty(settingProvider, value.Key, value.Value?.ToString());
            }
        }

        private void ChooseDestinationFolder()
        {
            var dialog = new VistaFolderBrowserDialog();
            dialog.Description = "Please choose destination folder.";
            dialog.UseDescriptionForTitle = true;

            if (dialog.ShowDialog() ?? false)
            {
                DestinationFolder = dialog.SelectedPath;
            }

            CanStart = CheckCanStart();
        }

        private void ChooseKeypair()
        {
            var ofd = new OpenFileDialog();
            ofd.Title = "Choose keypair to decrypt files";
            ofd.Filter = "Key files|*.key";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    _ = ArchiverWorker.ImportKeypair(ofd.FileName, false, true);
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
    }
}
