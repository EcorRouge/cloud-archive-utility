using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace EcorRouge.Archive.Utility.Settings
{
    public class SavedState
    {
        private string _connectorProperties;
        private string _pluginProperties;

        [JsonIgnore]
        public bool IsEmpty { get; set; }
        public int SelectedMode { get; set; }

        public long FilesProcessed { get; set; }
        public long BytesProcessed { get; set; }
        public string InputFileName { get; set; }
        public long TotalArchivedSize { get; set; }
        public long TotalFilesToArchive { get; set; }
        public long TotalFilesSizeToArchive { get; set; }
        public string ManifestFileName { get; set; }
        public string ArchiveFileName { get; set; }

        public string PluginType { get; set; }

        public string ConnectorType { get; set; }

        public string PluginProperties
        {
            get => _pluginProperties;
            set => _pluginProperties = value;
        }

        public string ConnectorProperties
        {
            get => _connectorProperties;
            set => _connectorProperties = value;
        }

        public bool EncryptFiles { get; set; }
        public string KeypairFilename { get; set; }
        public bool DeleteFiles { get; set; }
        public int MaximumFiles { get; set; }
        public int MaximumArchiveSizeMb { get; set; }

        public void SetPluginProperties(Dictionary<string, object> pluginProperties) => SetProperties(ref _pluginProperties, pluginProperties);

        public Dictionary<string, object> GetPluginProperties() => GetProperties(PluginProperties);

        public void SetConnectorProperties(Dictionary<string, object> connectorProperties) => SetProperties(ref _connectorProperties, connectorProperties);

        public Dictionary<string, object> GetConnectorProperties() => GetProperties(ConnectorProperties);

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
        private static string FileName => Path.Combine(PathHelper.GetRootDataPath(true), "saved_state.json");

        public static SavedState Load()
        {
            var path = FileName;
            if (!File.Exists(path))
            {
                return new SavedState()
                {
                    IsEmpty = true
                };
            }

            var result = JsonConvert.DeserializeObject<SavedState>(File.ReadAllText(path));
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
