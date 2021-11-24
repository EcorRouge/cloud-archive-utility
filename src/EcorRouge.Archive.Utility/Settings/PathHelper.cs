using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace EcorRouge.Archive.Utility.Settings
{
    public class PathHelper
    {
        internal static readonly ILog log = LogManager.GetLogger(typeof(PathHelper));

        public static string GetRootDataPath(bool create = false)
        {
            var dataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var rootPath = Path.Combine(dataPath, "EcorRouge", "ArchiveUtility");

            if (create && !Directory.Exists(rootPath))
                Directory.CreateDirectory(rootPath);

            return rootPath;
        }

        private static string GetDataFolderPath(string name, bool create)
        {
            var folderPath = Path.Combine(GetRootDataPath(create), name);
            try
            {
                if (create && !Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating {folderPath}: {ex.Message}\n{ex}");
            }

            return folderPath;
        }

        public static string GetLogsPath(bool create = false)
        {
            return GetDataFolderPath("Logs", create);
        }

        public static string GetTempPath(bool create = false)
        {
            return GetDataFolderPath("Temp", create);
        }

        public static string GetTempDrive()
        {
            var path = GetRootDataPath();
            return path.Substring(0, path.IndexOf(":\\") + 2);
        }

        public static long GetFreeTempDriveSize()
        {
            try
            {
                var path = PathHelper.GetTempDrive();
                var drives = DriveInfo.GetDrives();

                foreach (var drive in drives)
                {
                    if (path.StartsWith(drive.Name, StringComparison.InvariantCultureIgnoreCase))
                        return drive.TotalFreeSpace;
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error reading free space: {ex.Message}", ex);
            }

            return 0;
        }

    }
}
