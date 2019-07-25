using Grpc.Core;
using SabberStoneClient;
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
        }

        public static void RunServerWith(int numberOfClients)
        {

            var port = 50051;

            GameServer server = new GameServer(port);
            server.Start();

            var matchMaker = server.GetMatchMakerService();
            matchMaker.Start(7);

            List<Task> clientTasks = new List<Task>();
            for (int i = 0; i < numberOfClients; i++)
            {
                clientTasks.Add(CreateGameClientTask(port, $"TestClient{i}", ""));
                Thread.Sleep(1000);
            }

            while (clientTasks.Any(p => !p.IsCompleted))
            {
                //Console.WriteLine("waiting...");
                Thread.Sleep(5000);
            }

            server.Stop();
            Console.ReadKey();

        }

        private static async Task CreateGameClientTask(int port, string accountName, string accountpsw, int numberOfGames = 0)
        {
            GameClient client = new GameClient(port, true);

            client.Connect();

            client.Register(accountName, accountpsw);

            Thread.Sleep(2000);

            if (client.GameClientState == GameClientState.Registred)
            {
                client.Queue();
            }

            var waiter = Task.Run(async () =>
            {
                while (client.GameClientState == GameClientState.Queued
                    || client.GameClientState == GameClientState.InGame)
                {
                    Thread.Sleep(2000);
                };

                await client.Disconnect();
                Console.WriteLine($"client[{accountName}]: disconnected.");
            });

            await waiter;
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
