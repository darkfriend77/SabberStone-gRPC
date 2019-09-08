using System;
using System.Collections.Generic;
using System.Text;
using SabberStoneServer.Core;
using SabberStoneContract.Core;
using SabberStoneContract.Client;

namespace SabberStoneXConsole
{
    public static class AIClientTester
    {
        public static async void Test2Client()
        {
            const int port = 50051;
            const string ip = "127.0.0.1";
            const string deck = @"AAEBAQcCrwSRvAIOHLACkQP/A44FqAXUBaQG7gbnB+8HgrACiLACub8CAA==";

            GameServer server = new GameServer(port);
            server.Start();

            var matchMaker = server.GetMatchMakerService();
            matchMaker.Start(1);

            AIClient client1 = await AIClient.Initialise(ip, port, new GameClient.Credential
            {
                Id = "Client1",
                Password = ""
            }, null, deck);
            AIClient client2 = await AIClient.Initialise(ip, port, new GameClient.Credential
            {
                Id = "Client2",
                Password = ""
            }, null, deck);

            Console.WriteLine(client1.Logs.ToString());
        }
    }
}
