using SabberStoneContract;
using SabberStoneContract.Client;
using SabberStoneContract.Core;
using SabberStoneContract.Interface;
using SabberStoneContract.Model;
using SabberStoneServer.Core;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace SabberStoneXConsole
{
    class Program
    {
        static void Main(string[] args)
        {

            RunServerWith(1);
            //FullTest();
            //RunServer();
            //VisitorTest();


            Console.ReadKey();
        }

        private static void VisitorTest()
        {

            RunServer();
            GameController testController1 = new GameController(new RandomAI());
            GameClient testClient1 = new GameClient("127.0.0.1", 50051, testController1);
            testClient1.Connect();
            testClient1.Register("TestClient1", "1234");

            Thread.Sleep(1000);

            GameController testController2 = new GameController(new RandomAI());
            GameClient testClient2 = new GameClient("127.0.0.1", 50051, testController2);
            testClient2.Connect();
            testClient2.Register("TestClient2", "1234");

            GameClient visitorClient1 = new GameClient("127.0.0.1", 50051, new GameController(new VisitorAI()));
            visitorClient1.Connect();
            visitorClient1.Register("VisitorClient1", "1234");

            Thread.Sleep(1000);

            visitorClient1.VisitAccount(true, "TestClient1");

            Thread.Sleep(1000);

            testClient1.Queue(GameType.Normal, "AAEBAQcCrwSRvAIOHLACkQP/A44FqAXUBaQG7gbnB+8HgrACiLACub8CAA==");

            Thread.Sleep(1000);

            testClient2.Queue(GameType.Normal, "AAEBAQcCrwSRvAIOHLACkQP/A44FqAXUBaQG7gbnB+8HgrACiLACub8CAA==");
                       
            Thread.Sleep(10000);

            visitorClient1.VisitAccount(false, "");

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

}
