using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using EcorRouge.Archive.Utility.Plugins;
using Microsoft.Toolkit.Mvvm.Input;
using Ookii.Dialogs.Wpf;

namespace EcorRouge.Archive.Utility.ViewModels
{
    public class PropertyModel : ObservableObject
    {
        private CloudProviderProperty _property;
        private string _value;

        public string Title => _property.Title;
        public string Name => _property.Name;
        public string Description => _property.Description;
        public bool Password => _property.PropertyType == CloudProviderPropertyType.Password;

        public bool IsText => _property.PropertyType == CloudProviderPropertyType.Password ||
                              _property.PropertyType == CloudProviderPropertyType.String;

        public bool IsFolder => _property.PropertyType == CloudProviderPropertyType.FolderPath;

        public ICommand ChooseFolderCommand => new RelayCommand(ChooseFolder);

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        private void ChooseFolder()
        {
            var dialog = new VistaFolderBrowserDialog();
            dialog.Description = "Please choose destination folder.";
            dialog.UseDescriptionForTitle = true;

            if (dialog.ShowDialog() ?? false)
            {
                Value = dialog.SelectedPath;
            }
        }

        public PropertyModel(CloudProviderProperty property, string value)
        {
            _property = property;
            Value = value;
        }
    }
}
