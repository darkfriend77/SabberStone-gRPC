using System.Reflection;
using Grpc.Core;
using log4net;
using SabberStoneContract.Core;
using SabberStoneServer.Services;

namespace SabberStoneServer.Core
{
    public class GameServer
    {
        private static readonly ILog Log = Logger.Instance.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly int _port;

        private readonly Server _server;

        private readonly GameServerServiceImpl _gameServerService;

        private readonly MatchMakerService _matchMakerService;

        public GameServer(int port = 50051)
        {
            _port = port;

            _gameServerService = new GameServerServiceImpl();

            _matchMakerService = new MatchMakerService(_gameServerService);

            _server = new Server
            {
                Services =
                {
                    GameServerService.BindService(_gameServerService)
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
            _matchMakerService.Stop();
            _server.ShutdownAsync().Wait();
        }

        public MatchMakerService GetMatchMakerService()
        {
            return _matchMakerService;
        }

    }
}
