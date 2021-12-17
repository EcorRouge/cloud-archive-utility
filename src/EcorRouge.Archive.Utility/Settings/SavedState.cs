using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace EcorRouge.Archive.Utility.Settings
{
    public class SavedState
    {
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
        public string PluginProperties { get; set; }
        public bool DeleteFiles { get; set; }
        public int MaximumFiles { get; set; }
        public int MaximumArchiveSizeMb { get; set; }

        public void SetPluginProperties(Dictionary<string, object> pluginProperties)
        {
            if (pluginProperties == null)
            {
                PluginProperties = null;
                return;
            }

            PluginProperties = StringProtection.EncryptString(JsonConvert.SerializeObject(pluginProperties));
        }

        public Dictionary<string, object> GetPluginProperties()
        {
            if (String.IsNullOrEmpty(PluginProperties))
                return new Dictionary<string, object>();

            return JsonConvert.DeserializeObject<Dictionary<string, object>>(
                StringProtection.DecryptString(PluginProperties));
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
