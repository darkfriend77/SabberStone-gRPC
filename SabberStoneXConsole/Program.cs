using Grpc.Core;
using SabberStoneClient;
using SabberStoneClient.Interface;
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
            RunServerWith(2);
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


            Console.WriteLine("Press any key to stop.");
            Console.ReadKey();
            server.Stop();
        }

        private static async Task<GameClient> CreateGameClientTask(int port, string accountName, string accountpsw, IGameAI sabberStoneAI, int numberOfGames = 0)
        {
            GameClient client = new GameClient(port, sabberStoneAI, accountName, true);

            client.Connect();

            //Thread.Sleep(2000);

            await client.Register(accountName, accountpsw);


            //Thread.Sleep(2000);

            if (client.GameClientState == GameClientState.Registred)
            {
                client.Queue();
            }

            client.StateChanged += Client_StateChanged;

            return client;
        }

        private static void Client_StateChanged(GameClient client, GameClientState state)
        {
            if (state == GameClientState.Queued
             || state == GameClientState.Invited
             || state == GameClientState.InGame) return;

            client.StateChanged -= Client_StateChanged;
            client.Disconnect();

            Console.WriteLine($"client[{client.AccountName}]: disconnected.");
        }


        //private static int index = 1;

        //private static async Task CreateClientTask(string target, GameServerStream gameServerStream, int sleepMs)
        //{
        //    var channel = new Channel(target, ChannelCredentials.Insecure);
        //    var client = new GameServerService.GameServerServiceClient(channel);

        //    // authentificate
        //    var reply1 = client.Authentication(new AuthRequest { AccountName = $"Test::{index++}", AccountPsw = string.Empty });

        //    using (var call = client.GameServerChannel(headers: new Metadata { new Metadata.Entry("token", reply1.SessionToken) }))
        //    {
        //        var request = Task.Run(async () =>
        //        {
        //            for (int i = 0; i < 10; i++)
        //            {
        //                Thread.Sleep(sleepMs);
        //                await call.RequestStream.WriteAsync(gameServerStream);
        //            }
        //        });
        //        var response = Task.Run(async () =>
        //        {
        //            while (await call.ResponseStream.MoveNext(CancellationToken.None))
        //            {
        //                Console.WriteLine($"{call.ResponseStream.Current.Message}");
        //            };
        //        });

        //        await request;
        //        //await response;
        //    }

        //}
    }
}
