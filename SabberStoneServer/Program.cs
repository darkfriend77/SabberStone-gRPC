using System;
using System.IO;
using System.Reflection;
using System.Xml;
using log4net;
using log4net.Config;

namespace SabberStoneServer
{
    public class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string LogConfigFile = @"log4net.config";

        public static void Main(string[] args)
        {
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo(LogConfigFile));


            var sabberStoneServer = new Core.GameServer();
            sabberStoneServer.Start();

            Log.Info("Press any key to stop the server...");
            Console.ReadKey();

            sabberStoneServer.Stop();
        }
    }
}
