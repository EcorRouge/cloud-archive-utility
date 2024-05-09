using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using EcorRouge.Archive.Utility.Plugins;
using log4net;
using McMaster.NETCore.Plugins;
using Reveles.Collector.Cloud.Connector;

namespace EcorRouge.Archive.Utility.CloudConnectors;

public class CloudConnectorsManager
{
    internal static readonly ILog log = LogManager.GetLogger(typeof(CloudConnectorsManager));

    private readonly List<ConnectorFacade> _connectors = new List<ConnectorFacade>();

    private readonly Dictionary<string, CloudProviderProperty[]> _connectorPropertyNamesByConnectorType = new();

    private readonly Dictionary<string, CloudProviderProperty[]> _connectorPropertyNamesByConnectorCommonType = new()
    {
        {
            "microsoft", new CloudProviderProperty[]
            {
                new() {Name = "clientId", Title = "Client ID", PropertyType = CloudProviderPropertyType.String},
                new() {Name = "clientSecret", Title = "Client Secret", PropertyType = CloudProviderPropertyType.Password},
                new() {Name = "tenant", Title = "Tenant", PropertyType = CloudProviderPropertyType.String}
            }
        }
    };

    private readonly Dictionary<string, char> _pathSeparatorsByConnectorType = new()
    {
        { "onedrive", ':' },
        { "msemail", ';'},
        { "sharepoint", ':'}
    };

    public List<ConnectorFacade> ConnectorsFacades => _connectors;

    public static CloudConnectorsManager Instance { get; } = new CloudConnectorsManager();

    public CloudConnectorsManager()
    {
        RegisterConnectors();
        log.Info($"Loaded {_connectors.Count} connectors: {string.Join(',', _connectors.Select(c => c.ConnectorType))}");
    }

    public ConnectorFacade GetConnectorFacade(int index)
    {
        if (index < 0 || index >= _connectors.Count) return null;

        return _connectors[index];
    }

    public ConnectorFacade GetConnectorFacade(string connectorType)
    {
        return _connectors.FirstOrDefault(c => string.Equals(c.ConnectorType, connectorType, StringComparison.OrdinalIgnoreCase));
    }

    public CloudProviderProperty[] GetCredentialsNames(ConnectorFacade connector)
    {
        if (_connectorPropertyNamesByConnectorType.TryGetValue(connector.ConnectorType, out var credsNames))
        {
            return credsNames;
        }

        return _connectorPropertyNamesByConnectorCommonType.TryGetValue(connector.ConnectorCommonType, out credsNames)
            ? credsNames
            : Array.Empty<CloudProviderProperty>();
    }

    private void RegisterConnectors()
    {
        var connectorsDir = Path.Combine(AppContext.BaseDirectory, "connectors");

        if (!Directory.Exists(connectorsDir))
            return;

        foreach (var dir in Directory.GetDirectories(connectorsDir))
        {
            var dirName = Path.GetFileName(dir);

            var connectorDll = Path.Combine(dir, dirName + ".dll");
            if (File.Exists(connectorDll))
            {
                Type connBaseType = typeof(ConnectorBase);

                var loader = PluginLoader.CreateFromAssemblyFile(
                    connectorDll,
                    sharedTypes: new[] { connBaseType });

                var connAsm = loader.LoadDefaultAssembly();

                foreach (var connector in CreateConnectors(connAsm, connBaseType))
                {
                    if (connector.Type.EndsWith("-old", StringComparison.InvariantCultureIgnoreCase)) continue;

                    char cloudPathSeparator = GetCloudPathSeparator(connector);
                    try
                    {
                        _connectors.Add(new ConnectorFacade(connector, cloudPathSeparator));
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Failed to load connector {connector.GetType().Name}. {ex}");
                    }
                }
            }
        }
    }

    private char GetCloudPathSeparator(ConnectorBase connector)
        => _pathSeparatorsByConnectorType.TryGetValue(connector.CommonType, out char separator)
            ? separator
            : ':';

    private IEnumerable<ConnectorBase> CreateConnectors(Assembly assembly, Type baseConnectorType)
    {
        var connectorTypes = assembly.GetTypes()
            .Where(t => baseConnectorType.IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var connType in connectorTypes)
        {
            yield return (ConnectorBase)Activator.CreateInstance(connType);
        }

        if (!assembly.GetName().Name?.EndsWith(".Ref") ?? true) yield break;

        foreach (var forwardedTypeName in GetForwardedTypes(assembly))
        {
            yield return (ConnectorBase)assembly.CreateInstance(forwardedTypeName);
        }
    }

    private IEnumerable<string> GetForwardedTypes(Assembly assembly)
    {
        using var fs = new FileStream(assembly.Location, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var peReader = new PEReader(fs);

        MetadataReader metadataReader = peReader.GetMetadataReader();

        foreach (ExportedTypeHandle typeHandle in metadataReader.ExportedTypes)
        {
            ExportedType type = metadataReader.GetExportedType(typeHandle);
            string ns = metadataReader.GetString(type.Namespace);
            string name = metadataReader.GetString(type.Name);
            yield return $"{ns}.{name}";
        }
    }
}