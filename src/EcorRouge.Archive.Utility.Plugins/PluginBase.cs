using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EcorRouge.Archive.Utility.Plugins
{
    public delegate void UploadProgressDelegate(long bytesUploaded, double percentUploaded);

    public delegate void ConnectorLogDelegate(LogLevel level, string message);

    public enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }

    public abstract class PluginBase
    {
        public event ConnectorLogDelegate OnLogMessage;
        public event UploadProgressDelegate OnUploadProgress;

        public abstract string ProviderName { get; }
        public abstract CloudProviderProperty[] Properties { get; }

        public abstract bool KeepSession { get; }
        public abstract bool VerifyProperties(Dictionary<string, object> properties);
        public abstract Task OpenSessionAsync(Dictionary<string, object> properties, CancellationToken cancellationToken = default);
        public abstract Task CloseSessionAsync(CancellationToken cancellationToken = default);
        public abstract Task UploadFileAsync(string fileName, CancellationToken cancellationToken = default);

        protected void UploadProgress(long bytesUploaded, double percentUploaded)
        {
            OnUploadProgress?.Invoke(bytesUploaded, percentUploaded);
        }

        protected void LogDebug(string message)
        {
            OnLogMessage?.Invoke(LogLevel.Debug, message);
        }

        protected void LogInfo(string message)
        {
            OnLogMessage?.Invoke(LogLevel.Info, message);
        }

        protected void LogWarn(string message)
        {
            OnLogMessage?.Invoke(LogLevel.Warn, message);
        }

        protected void LogError(string message)
        {
            OnLogMessage?.Invoke(LogLevel.Error, message);
        }

    }
}
