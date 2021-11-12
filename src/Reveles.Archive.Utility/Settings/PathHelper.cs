using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reveles.Archive.Utility.Settings
{
    public class PathHelper
    {
        public static string GetRootDataPath(bool create = false)
        {
            var dataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var rootPath = Path.Combine(dataPath, "Reveles", "ArchiveUtility");

            if (create && !Directory.Exists(rootPath))
                Directory.CreateDirectory(rootPath);

            return rootPath;
        }
    }
}
