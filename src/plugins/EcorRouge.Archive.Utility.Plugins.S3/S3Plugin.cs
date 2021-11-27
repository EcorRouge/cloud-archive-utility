using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;

namespace EcorRouge.Archive.Utility.Plugins.S3
{
    public class S3Plugin : PluginBase
    {
        private const int PART_SIZE = 50 * 1024 * 1024;
        private static CloudProviderProperty[] PROPERTIES = new CloudProviderProperty[]
        {
            new CloudProviderProperty()
            {
                Name = "aws_access_key",
                Title = "AWS Access Key",
                PropertyType = CloudProviderPropertyType.Password
            },
            new CloudProviderProperty()
            {
                Name = "aws_secret_key",
                Title = "AWS Secret Key",
                PropertyType = CloudProviderPropertyType.Password
            },
            new CloudProviderProperty()
            {
                Name = "aws_region",
                Title = "AWS Region",
                PropertyType = CloudProviderPropertyType.String
            },
            new CloudProviderProperty()
            {
                Name = "bucket",
                Title = "S3 bucket",
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

        public override string ProviderName => "Amazon S3";
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
            var requiredProps = new string[] { "aws_access_key", "aws_secret_key", "aws_region", "bucket" };

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
                //LogDebug("Using aws key: '" + properties["aws_access_key"] + "'");
                //LogDebug("Using aws secret: '" + properties["aws_secret_key"] + "'");
                //LogDebug("Using aws region: '" + properties["aws_region"] + "'");
                //LogDebug("Using aws bucket: '" + properties["bucket"] + "'");
                //LogDebug("Using aws prefix: '" + properties["prefix"] + "'");

                _client = new AmazonS3Client(properties["aws_access_key"]?.ToString(),
                    properties["aws_secret_key"]?.ToString(),
                    RegionEndpoint.GetBySystemName(properties["aws_region"]?.ToString()));

                _bucket = properties["bucket"]?.ToString() ?? "";
                _prefix = properties["prefix"]?.ToString() ?? "";
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
            var keyPath = (String.IsNullOrWhiteSpace(_prefix) ? "" : _prefix ) + Path.GetFileName(fileName);

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

        private void FileTransferUtilityRequest_UploadProgressEvent(object sender, UploadProgressArgs e)
        {
            UploadProgress(e.TransferredBytes, e.PercentDone);
        }
    }
}
