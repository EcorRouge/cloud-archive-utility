using System.Threading;
using System.Threading.Tasks;

namespace EcorRouge.Archive.Utility.CloudConnectors;

public interface IConnector
{
    string Prefix { get; }

    public string Type { get; }
    public bool IsCommon { get; }

    string CommonType { get; }
    IConnectionFactory ConnectionFactory { get; }

    Task DownloadResourceAsync(dynamic cloudConnection, string cloudPath, string filePath, CancellationToken cancellationToken = default);
}