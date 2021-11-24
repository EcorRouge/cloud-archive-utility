using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using EcorRouge.Archive.Utility.Plugins;

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

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public PropertyModel(CloudProviderProperty property, string value)
        {
            _property = property;
            Value = value;
        }
    }
}
