using Grpc.Core;
using Xunit;
using SabberStoneServer.Core;
using System.Collections.Generic;
using System.Threading.Tasks;
using SabberStoneContract.Core;

namespace SabberStoneXTest.ServerTest
{
    public class GameServerBasic
    {
        private readonly GameServer _server;

        private int _port = 50051;

        private string _target;

        public GameServerBasic()
        {
            _server = new GameServer(_port);
            _target = $"127.0.0.1:{_port}";
        }

        [Fact]
        public async Task GameServerChannelAsync()
        {
            _server.Start();

            var channel1 = new Channel(_target, ChannelCredentials.Insecure);
            Assert.Equal(ChannelState.Idle, channel1.State);

            var client1 = new GameServerService.GameServerServiceClient(channel1);
            Assert.Equal(ChannelState.Idle, channel1.State);

            var reply1 = client1.Authentication(new AuthRequest { AccountName = "Test", AccountPsw = string.Empty });

            Assert.True(reply1.RequestState);
            Assert.Equal(10000, reply1.SessionId);

            using (var call = client1.GameServerChannel(headers: new Metadata { new Metadata.Entry("token", reply1.SessionToken) }))
            {
                await call.RequestStream.WriteAsync(new GameServerStream
                {
                    MessageType = MsgType.Initialisation,
                    Message = string.Empty
                });

                Assert.True(await call.ResponseStream.MoveNext());
                Assert.Equal(MsgType.Initialisation, call.ResponseStream.Current.MessageType);
                Assert.True(call.ResponseStream.Current.MessageState);
            }

            _server.Stop();
        }
    }
}
