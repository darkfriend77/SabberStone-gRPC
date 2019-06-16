using System;
using System.Reflection;
using Grpc.Core;
using log4net;
using log4net.Config;
using SabberStoneClient.Core;

namespace SabberStoneClient
{
    public class Program
    {
        private static readonly ILog Log = Logger.Instance.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        static void Main(string[] args)
        {
            //var channel = new Channel("127.0.0.1:50051", ChannelCredentials.Insecure);

            //var client = new TestgRPC.TestgRPCClient(channel);
            //var user = "me";

            //var reply = client.SayHello(new HelloRequest { Name = user });
            //Console.WriteLine($"Greeting: {reply.Message}");

            //channel.ShutdownAsync().Wait();
            //Console.WriteLine("Press any key to exit...");
            //Console.ReadKey();


        }
    }
}
