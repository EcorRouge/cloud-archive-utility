using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using Newtonsoft.Json;

namespace EcorRouge.Archive.Utility.Settings
{
    public class SettingsFile
    {
        public const string DefaultConnectorType = "Local Disk";

        internal static readonly ILog log = LogManager.GetLogger(typeof(SettingsFile));

        private static SettingsFile _instance = null;
        private static object _instanceLock = new object();

        public static SettingsFile Instance
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        _instance = Load();
                    }

                    return _instance;
                }
            }
        }

        [JsonIgnore]
        public string FileName { get; private set; }

        public Dictionary<string, string> PreviousCredentials { get; set; }

        public int ProviderIndex { get; set; }

        public string ConnectorType { get; set; } = DefaultConnectorType;

        public bool DeleteFilesAfterUpload { get; set; }

        public int MaximumFiles { get; set; }

        public int MaximumArchiveSizeMb { get; set; }

        private string MakeKey(string provider, string key)
        {
            return
                $"{provider}:{key}";
        }

        public void AddProviderProperty(string provider, string key, string value)
        {
            var dictionaryKey = MakeKey(provider, key);

            if (PreviousCredentials == null)
            {
                PreviousCredentials = new Dictionary<string, string>();
            }

            if (String.IsNullOrWhiteSpace(value))
            {
                PreviousCredentials.Remove(dictionaryKey);
            }
            else
            {
                PreviousCredentials[dictionaryKey] = StringProtection.EncryptString(value);
            }
        }

        public string GetProviderProperty(string provider, string key, string defaultValue = null)
        {
            var dictionaryKey = MakeKey(provider, key);

            if (PreviousCredentials?.ContainsKey(dictionaryKey) ?? false)
            {
                return StringProtection.DecryptString(PreviousCredentials[dictionaryKey]);
            }

            return defaultValue;
        }

        private static SettingsFile CreateEmptySettings()
        {
            return new SettingsFile()
            {
                ProviderIndex = 0,
                ConnectorType = DefaultConnectorType,
                DeleteFilesAfterUpload = true,
                MaximumFiles = 1000,
                MaximumArchiveSizeMb = 2048,
                PreviousCredentials = new Dictionary<string, string>()
            };
        }

        private static SettingsFile Load()
        {
            var rootPath = PathHelper.GetRootDataPath(true);

            var path = Path.Combine(rootPath, "settings.json");
            if (!File.Exists(path))
            {
                var settings = CreateEmptySettings();
                settings.FileName = path;
                settings.Save();
            }

            var result = JsonConvert.DeserializeObject<SettingsFile>(File.ReadAllText(path));
            result.FileName = path;

            return result;
        }

        public void Save()
        {
            File.WriteAllText(FileName, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
