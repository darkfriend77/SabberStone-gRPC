
using SabberStoneClient;
using SabberStoneContract;
using SabberStoneContract.Client;
using SabberStoneContract.Core;
using SabberStoneContract.Interface;
using SabberStoneServer.Core;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SabberStoneXConsole
{
    class Program
    {
        static void Main(string[] args)
        {

            RunServerWith(1);
            //FullTest();
            //RunServer();

            Console.ReadKey();
        }

        public static void RunServer()
        {
            var port = 50051;

            GameServer server = new GameServer(port);
            server.Start();

            var matchMaker = server.GetMatchMakerService();
            matchMaker.Start(1);
        }

        public static void DisconnectTest()
        {
            GameClient client = new GameClient("127.0.0.1", 50051, new GameController(new RandomAI()));

            client.Connect();
            client.Disconnect();
        }

        public static void FullTest()
        {
            int playerCount = 6;
            int gameCount = 10;

            Stopwatch stopWatch = new Stopwatch();

            stopWatch.Start();

            for (int i = 0; i < gameCount; i++)
            {
                RunServerWith(playerCount);
            }

            stopWatch.Stop();

            TimeSpan ts = stopWatch.Elapsed;

            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);

            Console.WriteLine($"### RunTime {playerCount} players {gameCount} games: " + elapsedTime);

            Console.ReadKey();
        }

        public static void SimpleTest()
        {
            int port = 50051;
            GameClient client = new GameClient("127.0.0.1", port, new GameController(new RandomAI()));
            Console.WriteLine(client.GameClientState);
            client.Connect();
            Console.WriteLine(client.GameClientState);
            client.Disconnect();
            Console.WriteLine(client.GameClientState);
            Console.ReadKey();
        }

        public static void RunServerWith(int numberOfClients)
        {

            var port = 50051;

            GameServer server = new GameServer(port);
            server.Start();

            var matchMaker = server.GetMatchMakerService();
            matchMaker.Start(1);

            GameClient[] tasks = new GameClient[numberOfClients];

            for (int i = 0; i < numberOfClients; i++)
            {
                int index = i;

                tasks[index] = CreateGameClientTask("127.0.0.1", port, $"TestClient{index}", "", new RandomAI());
            }

            while (tasks.Any(p => p.GameClientState != GameClientState.None))
            {
                Thread.Sleep(100);
            }

            server.Stop();
        }

        private static GameClient CreateGameClientTask(string targetIp, int port, string accountName, string accountpsw, IGameAI gameAI, int numberOfGames = 0)
        {

            GameClient client = new TestClient(accountName, targetIp, port, new TestGameController(gameAI));

            client.Connect();

            return client;
        }

    }

    public class TestClient : GameClient
    {
        private string _accountName;
        public TestClient(string accountName, string targetIp, int port, GameController gameController) : base(targetIp, port, gameController)
        {
            _accountName = accountName;
        }

        public override void CallGameClientState(GameClientState oldState, GameClientState newState)
        {
            switch (newState)
            {
                case GameClientState.None:
                    break;

                case GameClientState.Connected:
                    Register(_accountName, "");
                    break;

                case GameClientState.Registred:
                    if (oldState != GameClientState.InGame)
                    {
                        Thread.Sleep(200);
                        Queue(GameType.Normal, "AAEBAQcCrwSRvAIOHLACkQP/A44FqAXUBaQG7gbnB+8HgrACiLACub8CAA==");
                        //Queue();
                        // [EU Legend #1 Tempo Mage] AAEBAf0ECnH2Ar8D7AW5BuwHuQ36Dp4QixQKwwG7ApUD5gSWBfcNgQ6HD4kPkBAA
                    }
                    else
                    {
                        Disconnect();
                    }
                    break;

                case GameClientState.Queued:
                    break;
                case GameClientState.Invited:
                    break;
                case GameClientState.InGame:
                    break;
            }
        }

        private void Register()
        {
            throw new NotImplementedException();
        }
    }
}
