using Grpc.Core;
using Xunit;
using SabberStoneServer.Core;

namespace SabberStoneXTest.ServerTest
{
    public class GameServerBasic
    {
        private readonly GameServer _server;

        public GameServerBasic()
        {
            _server = new GameServer();
        }

        [Fact]
        public void PingRequest()
        {
            _server.Start();

            var channel = new Channel("127.0.0.1:50051", ChannelCredentials.Insecure);
            Assert.Equal(ChannelState.Idle, channel.State);

            var client = new GameServerService.GameServerServiceClient(channel);
            Assert.Equal(ChannelState.Idle, channel.State);

            var reply = client.Ping(new PingRequest { Message = string.Empty });

            Assert.True(reply.RequestState);
            Assert.Equal("Ping", reply.RequestMessage);

            _server.Stop();
        }

        [Fact]
        public void AuthRequest()
        {
            _server.Start();

            var channel = new Channel("127.0.0.1:50051", ChannelCredentials.Insecure);
            Assert.Equal(ChannelState.Idle, channel.State);

            var client = new GameServerService.GameServerServiceClient(channel);
            Assert.Equal(ChannelState.Idle, channel.State);

            var reply = client.Authentication(new AuthRequest { AccountName = "Test", AccountPsw = string.Empty });

            Assert.True(reply.RequestState);
            Assert.Equal(10000, reply.SessionId);
            Assert.Equal("58887f83a90f6f972888fcc5f66b6cd6069e5e665883404316f30828125352f4", reply.SessionToken);
  
            _server.Stop();
        }
    }
}
