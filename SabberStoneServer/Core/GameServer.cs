using System.Reflection;
using Grpc.Core;
using log4net;
using SabberStoneServer.Services;

namespace SabberStoneServer.Core
{
    public class GameServer
    {
        private static readonly ILog Log = Logger.Instance.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly int _port;

        private readonly Server _server;

        public GameServer(int port = 50051)
        {
            _port = port;

            _server = new Server
            {
                Services =
                {
                    GameServerService.BindService(new GameServerServiceImpl())
                },
                Ports =
                {
                    new ServerPort("localhost", _port, ServerCredentials.Insecure)
                }
            };
        }

        public void Start()
        {
            Log.Info($"GameServer listening on port {_port}");
            _server.Start();
        }

        public void Stop()
        {
            _server.ShutdownAsync().Wait();
        }
    }
}
