using Grpc.Core;
using SabberStoneClient;
using SabberStoneClient.AI;
using SabberStoneClient.Core;
using SabberStoneServer.Core;
using System;
using System.Collections.Generic;
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
            GameClient client = new GameClient(port, new GameController(new RandomAI()));
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

            Task<GameClient>[] tasks = new Task<GameClient>[numberOfClients];

            for (int i = 0; i < numberOfClients; i++)
            {
                int index = i;

                tasks[index] = CreateGameClientTask(port, $"TestClient{index}", "", new RandomAI());
            }

            while (tasks.Any(p => !p.IsCompleted))
            {
                Thread.Sleep(100);
            }

            server.Stop();
        }

        private static async Task<GameClient> CreateGameClientTask(int port, string accountName, string accountpsw, ISabberStoneAI sabberStoneAI, int numberOfGames = 0)
        {
            GameClient client = new GameClient(port, new GameController(sabberStoneAI));

            client.Connect();

            client.Register(accountName, accountpsw);

            Thread.Sleep(200);

            client.Queue();

            var taskWaiter = Task.Run(() =>
            {
                var oldState = client.GameClientState;
                while (client.GameClientState != GameClientState.Registred)
                {
                    Thread.Sleep(200);
                }
                client.Disconnect();

                Console.WriteLine($"client[{accountName}]: disconnected.");
            });

            await taskWaiter;

            return client;
        }

    }
}
