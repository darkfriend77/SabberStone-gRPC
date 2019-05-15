using System;
using System.Threading.Tasks;
using Grpc.Core;

namespace SabberStoneServer
{
    internal class TestgRpcImpl : TestgRPC.TestgRPCBase
    {
        // Server side handler of the SayHello RPC
        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            Console.WriteLine(context.Peer);

            var reply = new HelloReply {Message = $"Hello {request.Name}"};

            return Task.FromResult(reply);
        }
    }

    public class Program
    {
        private const int Port = 50051;

        public static void Main(string[] args)
        {
            var server = new Server
            {
                Services = { TestgRPC.BindService(new TestgRpcImpl()) },
                Ports = { new ServerPort("localhost", Port, ServerCredentials.Insecure) }
            };
            server.Start();

            Console.WriteLine($"Greeter server listening on port {Port}");
            Console.WriteLine("Press any key to stop the server...");
            Console.ReadKey();

            server.ShutdownAsync().Wait();
        }
    }
}
