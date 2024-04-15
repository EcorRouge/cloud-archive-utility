using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ImpromptuInterface;

namespace EcorRouge.Archive.Utility.CloudConnectors;

public class ConnectorFacade
{
    private readonly IConnector _connector;
    private ICloudConnection _cloudConnection;

    public ConnectorFacade(IConnector connector)
    {
        _connector = connector;
    }

    public string CredsType => _connector.IsCommon ? _connector.CommonType : _connector.Type;

    public string ConnectorType => _connector.Type;

    public string ConnectorCommonType => _connector.CommonType;

    public string Prefix => _connector.Prefix;

    public void Connect(Dictionary<string, string> credentials) // no params needed - use Credentials prop?
    {
        var cloudCreds = _connector.ConnectionFactory.TranslateCredentials(credentials);
        _cloudConnection = _connector.ConnectionFactory.Connect(cloudCreds.UndoActLike());
    }

    public Task DownloadAsync(string cloudPath, string filePath, CancellationToken cancellationToken)
    {
        return _connector.DownloadResourceAsync(_cloudConnection.UndoActLike(), cloudPath, filePath, cancellationToken);
    }
}