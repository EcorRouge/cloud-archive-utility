using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using EcorRouge.Archive.Utility.Settings;

namespace EcorRouge.Archive.Utility
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Setups application logging
        /// <param name="fileName">Log file name</param>
        /// <param name="needDebug">If true, writes debug info to log file</param>
        /// </summary>
        private static void Log4Setup(bool needDebug, bool needConsole = false)
        {
            var repo = LogManager.GetRepository(Assembly.GetEntryAssembly());

            var logsPath = PathHelper.GetLogsPath(true);

            if (!repo.Configured)
            {
                Logger root = ((Hierarchy)repo).Root;
                root.Level = needDebug ? Level.All : Level.Info;

                if (Debugger.IsAttached || needConsole)
                {
                    var ca = new ConsoleAppender();
                    ca.Layout = new PatternLayout("%d{dd.MM.yyyy HH:mm:ss} [%property{threadName}] %-5p %m%n");
                    root.AddAppender(ca);
                }

                var fa = new RollingFileAppender();

                fa.Layout = new PatternLayout("%d{dd.MM.yyyy HH:mm:ss} [%property{threadName}] %-5p %m%n");
                fa.File = Path.Combine(logsPath, "archive_utility.log");
                fa.ImmediateFlush = true;
                fa.AppendToFile = true;
                fa.RollingStyle = RollingFileAppender.RollingMode.Size;
                fa.MaxSizeRollBackups = 3;
                fa.MaxFileSize = 10 * 1024 * 1024;
                fa.ActivateOptions();
                root.AddAppender(fa);

                repo.Configured = true;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // For ZipOutputStream and IBM437 encoding

            Log4Setup(true, false);
        }
    }
}
