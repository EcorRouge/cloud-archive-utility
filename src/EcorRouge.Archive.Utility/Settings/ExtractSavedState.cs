using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EcorRouge.Archive.Utility.Settings
{
    public class ExtractSavedState
    {
        private string _pluginProperties;

        [JsonIgnore]
        public bool IsEmpty { get; set; }

        public string InputFilename { get; set; }
        public string SearchExpression { get; set; }        
        public string DestinationFolder { get; set; }

        public string PluginType { get; set; }
        public string PluginProperties
        {
            get => _pluginProperties;
            set => _pluginProperties = value;
        }
        
        public string KeypairFilename { get; set; }

        public bool IsEncrypted { get; set; }
        public string CurrentZipFileName { get; set; }        
        public string CurrentEntryFileName { get; set; }

        public void SetPluginProperties(Dictionary<string, object> pluginProperties) => SetProperties(ref _pluginProperties, pluginProperties);

        public Dictionary<string, object> GetPluginProperties() => GetProperties(PluginProperties);

        public void SetProperties(ref string properties, Dictionary<string, object> connectorProperties)
        {
            if (connectorProperties == null)
            {
                properties = null;
                return;
            }

            properties = StringProtection.EncryptString(JsonConvert.SerializeObject(connectorProperties));
        }

        private Dictionary<string, object> GetProperties(string properties)
        {
            if (String.IsNullOrEmpty(properties))
                return new Dictionary<string, object>();

            return JsonConvert.DeserializeObject<Dictionary<string, object>>(StringProtection.DecryptString(properties));
        }

        [JsonIgnore]
        private static string FileName => Path.Combine(PathHelper.GetRootDataPath(true), "extract_saved_state.json");

        public static ExtractSavedState Load()
        {
            var path = FileName;
            if (!File.Exists(path))
            {
                return new ExtractSavedState()
                {
                    IsEmpty = true
                };
            }

            var result = JsonConvert.DeserializeObject<ExtractSavedState>(File.ReadAllText(path));
            result.IsEmpty = false;

            return result;
        }

        public static void Clear()
        {
            File.Delete(FileName);
        }

        public void Save()
        {
            File.WriteAllText(FileName, JsonConvert.SerializeObject(this, Formatting.Indented));
        }

    }
}
