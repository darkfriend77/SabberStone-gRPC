using System;
using System.Reflection;
using log4net;
using SabberStoneServer.Core;

namespace SabberStoneServer
{
    public class Program
    {
        private static readonly ILog Log = Logger.Instance.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static void Main(string[] args)
        {
            var sabberStoneServer = new GameServer();
            sabberStoneServer.Start();

            var matchMaker = sabberStoneServer.GetMatchMakerService();
            matchMaker.Start(1);
            Log.Info("Press any key to stop the server...");
            Console.ReadKey();

            sabberStoneServer.Stop();
        }
    }
}
