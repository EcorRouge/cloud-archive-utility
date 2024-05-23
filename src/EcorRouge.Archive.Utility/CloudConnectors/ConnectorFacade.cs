using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CSharp.RuntimeBinder;
using Reveles.Collector.Cloud.Connector;

namespace EcorRouge.Archive.Utility.CloudConnectors;

public class ConnectorFacade
{
    private readonly ConnectorBase _connector;
    private ICloudConnection _cloudConnection;

    public ConnectorFacade(ConnectorBase connector, char cloudPathSeparator)
    {
        _connector = connector;
        CloudPathSeparator = cloudPathSeparator;

        var prefixes = new List<string>() {_connector.Type};

        try
        {
            prefixes.Add((string) ((dynamic) _connector).Prefix);
        }
        catch (RuntimeBinderException)
        {
            throw new NotSupportedException(
                $"Connector {_connector.GetType().Name} is not supported - only connectors prefixing their results (see Prefix property) are currently supported.");
        }

        prefixes.Add(_connector.Type);

        Prefixes = prefixes.ToArray();
    }

    public char CloudPathSeparator { get; }

    public string CredsType => _connector.IsCommon ? _connector.CommonType : _connector.Type;

    public string ConnectorType => _connector.Type;

    public string ConnectorCommonType => _connector.CommonType;

    public string[] Prefixes { get; }

    public void Connect(Dictionary<string, string> credentials) // no params needed - use Credentials prop?
    {
        var cloudCreds = _connector.ConnectionFactory.TranslateCredentials(credentials);
        _cloudConnection = _connector.ConnectionFactory.Connect(cloudCreds);
    }

    public Task DownloadAsync(string cloudPath, string filePath, CancellationToken cancellationToken)
    {
        return _connector.DownloadResourceAsync(_cloudConnection, cloudPath, filePath, cancellationToken);
    }

    public Task DeleteAsync(string cloudPath, CancellationToken cancellationToken)
    {
        return _connector.DeleteResourceAsync(_cloudConnection, cloudPath, cancellationToken);
    }
}