using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using EcorRouge.Archive.Utility.Plugins;
using ImpromptuInterface;
using log4net;
using McMaster.NETCore.Plugins;

namespace EcorRouge.Archive.Utility.CloudConnectors;

public class CloudConnectorsManager
{
    internal static readonly ILog log = LogManager.GetLogger(typeof(CloudConnectorsManager));

    private readonly List<ConnectorFacade> _connectors = new List<ConnectorFacade>();

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

    private void RegisterConnectors()
    {
        var connectorsDir = Path.Combine(AppContext.BaseDirectory, "connectors");

        if (!Directory.Exists(connectorsDir))
            return;

        const string connBaseDirName = "Reveles.Collector.Cloud.Connector.Ref";
        var connBaseDllPath = Path.Combine(connectorsDir, connBaseDirName, "Reveles.Collector.Cloud.Connector.dll");
        if (!File.Exists(connBaseDllPath))
            return;

        foreach (var dir in Directory.GetDirectories(connectorsDir))
        {
            var dirName = Path.GetFileName(dir);
            if (dirName.Equals(connBaseDirName, StringComparison.OrdinalIgnoreCase)) continue;

            var pluginDll = Path.Combine(dir, dirName + ".dll");
            if (File.Exists(pluginDll))
            {
                //var loader = PluginLoader.CreateFromAssemblyFile(pluginDll, sharedTypes: new[] {connBaseType});

                var loader = PluginLoader.CreateFromAssemblyFile(pluginDll);
                var connBaseAsm = loader.LoadAssemblyFromPath(connBaseDllPath);
                var connAsm = loader.LoadDefaultAssembly();

                Type connBaseType = connBaseAsm.GetType("Reveles.Collector.Cloud.Connector.ConnectorBase");
                foreach (var connector in CreateConnectors(connAsm, connBaseType))
                {
                    if (connector.Type.EndsWith("-old", StringComparison.InvariantCultureIgnoreCase)) continue;

                    char cloudPathSeparator = GetCloudPathSeparator(connector);
                    _connectors.Add(new ConnectorFacade(connector, cloudPathSeparator));
                }
            }
        }
    }

    private char GetCloudPathSeparator(IConnector connector)
        => _pathSeparatorsByConnectorType.TryGetValue(connector.CommonType, out char separator)
            ? separator
            : ':';

    private IEnumerable<IConnector> CreateConnectors(Assembly assembly, Type baseConnectorType)
    {
        var connectorTypes = assembly.GetTypes()
            .Where(t => baseConnectorType.IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var connType in connectorTypes)
        {
            yield return Activator.CreateInstance(connType).ActLike<IConnector>();
        }

        if (!assembly.GetName().Name?.EndsWith(".Ref") ?? true) yield break;

        foreach (var forwardedTypeName in GetForwardedTypes(assembly))
        {
            yield return assembly.CreateInstance(forwardedTypeName).ActLike<IConnector>();
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