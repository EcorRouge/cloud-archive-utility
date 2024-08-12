using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

namespace EcorRouge.Archive.Utility.Plugins.Wasabi
{
    public class WasabiPlugin : PluginBase
    {
        private const int PART_SIZE = 50 * 1024 * 1024;

        private static CloudProviderProperty[] PROPERTIES = new CloudProviderProperty[]
        {
            new CloudProviderProperty()
            {
                Name = "access_key",
                Title = "Access Key",
                PropertyType = CloudProviderPropertyType.Password
            },
            new CloudProviderProperty()
            {
                Name = "secret_key",
                Title = "Secret Key",
                PropertyType = CloudProviderPropertyType.Password
            },
            new CloudProviderProperty()
            {
                Name = "region",
                Title = "Region",
                PropertyType = CloudProviderPropertyType.String
            },
            new CloudProviderProperty()
            {
                Name = "bucket",
                Title = "Bucket name",
                PropertyType = CloudProviderPropertyType.String
            },
            new CloudProviderProperty()
            {
                Name = "prefix",
                Title = "Path prefix",
                PropertyType = CloudProviderPropertyType.String
            },
        };

        private object _lock = new object();
        private AmazonS3Client _client;
        private string _bucket;
        private string _prefix;

        public override string ProviderName => "Wasabi";
        public override CloudProviderProperty[] Properties => PROPERTIES;
        public override bool KeepSession => true;

        public override async Task<bool> TryConnectAndWriteSmallFile(Dictionary<string, object> properties, CancellationToken cancellationToken = default)
        {
            try
            {
                await OpenSessionAsync(properties, cancellationToken);

                var fileName = Path.Combine(Path.GetTempPath(), "ecorrouge-upload-test.txt");
                File.WriteAllText(fileName, $"{DateTime.Now:F}");

                await UploadFileAsync(fileName, cancellationToken);
            }
            finally
            {
                await CloseSessionAsync(cancellationToken);
            }

            return true;
        }

        public override bool VerifyProperties(Dictionary<string, object> properties)
        {
            var requiredProps = new string[] { "access_key", "secret_key", "region", "bucket" };

            foreach (var requiredProp in requiredProps)
            {
                if (!properties.ContainsKey(requiredProp))
                    return false;

                if (properties[requiredProp] == null || String.IsNullOrWhiteSpace(properties[requiredProp].ToString()))
                    return false;
            }

            return true;
        }

        public override Task OpenSessionAsync(Dictionary<string, object> properties, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                //LogDebug("Using key: '" + properties["access_key"] + "'");
                //LogDebug("Using secret: '" + properties["secret_key"] + "'");
                //LogDebug("Using region: '" + properties["region"] + "'");
                //LogDebug("Using bucket: '" + properties["bucket"] + "'");
                //LogDebug("Using prefix: '" + properties["prefix"] + "'");

                AmazonS3Config config;

                var region = properties["region"]?.ToString();
                if(String.IsNullOrWhiteSpace(region))
                {
                    config = new AmazonS3Config { ServiceURL = "https://s3.wasabisys.com" };
                } else
                {
                    config = new AmazonS3Config { ServiceURL = $"https://s3.{region}.wasabisys.com" };
                }

                _client = new AmazonS3Client(properties["access_key"]?.ToString(),
                    properties["secret_key"]?.ToString(),
                    config);

                _bucket = properties["bucket"]?.ToString() ?? "";
                _prefix = properties["prefix"]?.ToString() ?? "";

                if (!String.IsNullOrWhiteSpace(_prefix) && !_prefix.EndsWith("/"))
                {
                    _prefix += "/";
                }
            }

            return Task.CompletedTask;
        }

        public override Task CloseSessionAsync(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (_client == null)
                    return Task.CompletedTask;

                _client.Dispose();
                _client = null;
            }

            return Task.CompletedTask;
        }

        public override async Task UploadFileAsync(string fileName, CancellationToken cancellationToken = default)
        {
            var keyPath = (String.IsNullOrWhiteSpace(_prefix) ? "" : _prefix) + Path.GetFileName(fileName);

            var fileTransferUtility = new TransferUtility(_client);

            LogDebug($"Uploading {fileName} to {keyPath}...");

            var fileTransferUtilityRequest = new TransferUtilityUploadRequest
            {
                BucketName = _bucket,
                FilePath = fileName,
                StorageClass = S3StorageClass.StandardInfrequentAccess,
                PartSize = PART_SIZE,
                Key = keyPath,
                //CannedACL = S3CannedACL.NoACL
            };

            fileTransferUtilityRequest.UploadProgressEvent += FileTransferUtilityRequest_UploadProgressEvent;

            await fileTransferUtility.UploadAsync(fileTransferUtilityRequest, cancellationToken);

            File.Delete(fileName);
        }

        public override async Task DownloadFileAsync(string fileName, string destinationFileName, CancellationToken cancellationToken = default)
        {
            var keyPath = (String.IsNullOrWhiteSpace(_prefix) ? "" : _prefix) + Path.GetFileName(fileName);

            var fileTransferUtility = new TransferUtility(_client);

            LogDebug($"Uploading {fileName} to {keyPath}...");

            var fileTransferUtilityRequest = new TransferUtilityDownloadRequest
            {
                BucketName = _bucket,
                Key = keyPath,
                FilePath = destinationFileName,
            };

            fileTransferUtilityRequest.WriteObjectProgressEvent += FileTransferUtilityRequest_DownloadProgressEvent;

            await fileTransferUtility.DownloadAsync(fileTransferUtilityRequest, cancellationToken);

            File.Delete(fileName);
        }

        private void FileTransferUtilityRequest_DownloadProgressEvent(object sender, WriteObjectProgressArgs e)
        {
            DownloadProgress(e.TransferredBytes, e.PercentDone);
        }

        private void FileTransferUtilityRequest_UploadProgressEvent(object sender, UploadProgressArgs e)
        {
            UploadProgress(e.TransferredBytes, e.PercentDone);
        }
    }
}
