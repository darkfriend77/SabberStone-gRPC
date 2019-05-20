﻿using Grpc.Core;
using SabberStoneServer.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SabberStoneXConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var port = 50051;
            var target = $"127.0.0.1:{port}";

            var server = new GameServer();
            server.Start();

            Task clientA = CreateClientTask(target, new GameServerStream()
            {
                SessionId = 1,
                SessionToken = "abc",
                Message = "testing"
            }, 2000);

            Task clientB = CreateClientTask(target, new GameServerStream()
            {
                SessionId = 2,
                SessionToken = "xyz",
                Message = "lying"
            }, 3000);

            Task clientC = CreateClientTask(target, new GameServerStream()
            {
                SessionId = 3,
                SessionToken = "ups",
                Message = "sabber"
            }, 4000);

            while(!clientA.IsCompleted || !clientB.IsCompleted || !clientC.IsCompleted)
            {
                Thread.Sleep(1000);
            }

            server.Stop();
            Console.ReadKey();
        }

        private static async Task CreateClientTask(string target, GameServerStream gameServerStream, int sleepMs)
        {
            var channel = new Channel(target, ChannelCredentials.Insecure);
            var client = new GameServerService.GameServerServiceClient(channel);
            using (var call = client.GameServerChannel())
            {
                var request = Task.Run(async () =>
                {
                    for (int i = 0; i < 10; i++)
                    {
                        Thread.Sleep(sleepMs);
                        await call.RequestStream.WriteAsync(gameServerStream);
                    }
                });
                var response = Task.Run(async () =>
                {
                    while (await call.ResponseStream.MoveNext(CancellationToken.None))
                    {
                        Console.WriteLine($"[Id:{call.ResponseStream.Current.SessionId}][Token:{call.ResponseStream.Current.SessionToken}]: {call.ResponseStream.Current.Message}.");
                    };
                });
                await request;
                await response;
            }
            await channel.ShutdownAsync();
        }
    }
}
