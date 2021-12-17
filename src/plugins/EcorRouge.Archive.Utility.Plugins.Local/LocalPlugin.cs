using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EcorRouge.Archive.Utility.Plugins.Local
{
    public class LocalPlugin : PluginBase
    {
        private static CloudProviderProperty[] PROPERTIES = new CloudProviderProperty[]
        {
            new CloudProviderProperty()
            {
                Name = "local_folder",
                Title = "Local Folder",
                PropertyType = CloudProviderPropertyType.FolderPath
            }
        };

        public override string ProviderName => "Local Disk";

        public override CloudProviderProperty[] Properties => PROPERTIES;

        public override bool KeepSession => true;

        private string _localFolder = null;

        public override async Task<bool> TryConnectAndWriteSmallFile(Dictionary<string, object> properties,
            CancellationToken cancellationToken = default)
        {
            var fileName = Path.Combine(Path.GetTempPath(), "ecorrouge-upload-test.txt");
            File.WriteAllText(fileName, $"{DateTime.Now:F}");

            await OpenSessionAsync(properties, cancellationToken);
            await UploadFileAsync(fileName, cancellationToken);

            return true;
        }

        public override bool VerifyProperties(Dictionary<string, object> properties)
        {
            var requiredProps = new string[] { "local_folder" };

            foreach (var requiredProp in requiredProps)
            {
                if (!properties.ContainsKey(requiredProp))
                    return false;

                if (properties[requiredProp] == null || String.IsNullOrWhiteSpace(properties[requiredProp].ToString()))
                    return false;
            }

            var localFolder = properties["local_folder"].ToString();

            return Directory.Exists(localFolder);
        }

        public override Task OpenSessionAsync(Dictionary<string, object> properties, CancellationToken cancellationToken = default)
        {
            _localFolder = properties["local_folder"].ToString();

            return Task.CompletedTask;
        }

        public override Task CloseSessionAsync(CancellationToken cancellationToken = default)
        {
            // Nothing
            return Task.CompletedTask;
        }

        public override Task UploadFileAsync(string fileName, CancellationToken cancellationToken = default)
        {
            return Task.Run(async () =>
            {
                var fInfo = new FileInfo(fileName);
                var buf = new byte[1024 * 1024 * 10];

                var drive = new DriveInfo(_localFolder.Substring(0, 1));
                if (drive.AvailableFreeSpace < fInfo.Length)
                {
                    throw new IOException($"Insufficient drive space on {drive.Name}");
                }

                var outputFileName = Path.Combine(_localFolder, Path.GetFileName(fileName));

                await using var reader = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                await using var writer = new FileStream(outputFileName, FileMode.Create, FileAccess.Write, FileShare.None);

                long totalBytes = 0;
                while (reader.CanRead)
                {
                    var len = await reader.ReadAsync(buf, 0, buf.Length, cancellationToken);
                    if (len <= 0)
                        break;

                    await writer.WriteAsync(buf, 0, len, cancellationToken);

                    totalBytes += len;

                    UploadProgress(totalBytes, totalBytes * 100.0 / fInfo.Length);
                }
            }, cancellationToken);
        }
    }
}
