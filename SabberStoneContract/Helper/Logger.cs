using log4net;
using log4net.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace SabberStoneContract.Helper
{
    public sealed class Logger
    {
        private static readonly string LogConfigFile = @"log4net.config";
        private static readonly Lazy<Logger> lazy = new Lazy<Logger>(() => new Logger());

        public static Logger Instance { get { return lazy.Value; } }

        private Logger()
        {
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo(LogConfigFile));
        }

        public ILog GetLogger(Type type)
        {
            return LogManager.GetLogger(type);
        }
    }
}
