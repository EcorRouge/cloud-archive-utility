using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;
using McMaster.NETCore.Plugins;
using EcorRouge.Archive.Utility.Plugins;

namespace EcorRouge.Archive.Utility
{
    public class PluginsManager
    {
        internal static readonly ILog log = LogManager.GetLogger(typeof(PluginsManager));

        readonly List<PluginBase> _plugins = new List<PluginBase>();

        public List<PluginBase> Plugins => _plugins;

        public static PluginsManager Instance { get; } = new PluginsManager();

        public PluginsManager()
        {
            RegisterPlugins();
            log.Info($"Loaded {_plugins.Count} plugins");
        }

        private void RegisterPlugins()
        {
            // Register default collector
            //collectors.Add(new SqlServerSupplyCollector());

            var loaders = new List<PluginLoader>();

            // create plugin loaders
            var pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");

            if (!Directory.Exists(pluginsDir))
                return;

            foreach (var dir in Directory.GetDirectories(pluginsDir))
            {
                var dirName = Path.GetFileName(dir);
                var pluginDll = Path.Combine(dir, dirName + ".dll");
                if (File.Exists(pluginDll))
                {
                    var loader = PluginLoader.CreateFromAssemblyFile(
                        pluginDll,
                        sharedTypes: new[] { typeof(PluginBase) });
                    loaders.Add(loader);
                }
            }

            // Create an instance of plugin types
            foreach (var loader in loaders)
            {
                foreach (var pluginType in loader
                    .LoadDefaultAssembly()
                    .GetTypes()
                    .Where(t => typeof(PluginBase).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    PluginBase plugin = (PluginBase)Activator.CreateInstance(pluginType);
                    _plugins.Add(plugin);
                }
            }
        }

    }
}
