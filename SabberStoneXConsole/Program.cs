using Grpc.Core;
using SabberStoneClient;
using SabberStoneClient.AI;
using SabberStoneClient.Core;
using SabberStoneServer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SabberStoneXConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            RunServerWith(20);
            //SimpleTest();
        }

        public static void SimpleTest()
        {
            int port = 50051;
            GameClient client = new GameClient(port, new RandomAI());
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
            matchMaker.Start(7);

            Task<GameClient>[] tasks = new Task<GameClient>[numberOfClients];

            for (int i = 0; i < numberOfClients; i++)
            {
                int index = i;

                tasks[index] = CreateGameClientTask(port, $"TestClient{index}", "", new RandomAI());
            }

            Console.ReadKey();
            server.Stop();
        }

        private static async Task<GameClient> CreateGameClientTask(int port, string accountName, string accountpsw, ISabberStoneAI sabberStoneAI, int numberOfGames = 0)
        {
            GameClient client = new GameClient(port, sabberStoneAI, accountName, false);

            client.Connect();

            client.Register(accountName, accountpsw);

            Thread.Sleep(1000);

            if (client.GameClientState == GameClientState.Registred)
            {
                client.Queue();
            }

            await Task.Run(() =>
            {
                var oldState = client.GameClientState;
                while (client.GameClientState != GameClientState.Registred)
                {
                    Thread.Sleep(1000);
                }
                client.Disconnect();

                Console.WriteLine($"client[{client.AccountName}]: disconnected.");
            });

            return client;
        }

    }
}
