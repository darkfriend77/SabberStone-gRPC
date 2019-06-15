using Grpc.Core;
using Xunit;
using SabberStoneServer.Core;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace SabberStoneXTest.ServerTest
{
    public class MatchMakerBasic
    {
        private readonly GameServer _server;

        private int _port = 50052;

        private string _target;

        public MatchMakerBasic()
        {
            _server = new GameServer(_port);
            _target = $"127.0.0.1:{_port}";
        }

        [Fact]
        public void MatchMakerTest()
        {
            _server.Start();

            var channel1 = new Channel(_target, ChannelCredentials.Insecure);
            Assert.Equal(ChannelState.Idle, channel1.State);

            var client1 = new GameServerService.GameServerServiceClient(channel1);
            Assert.Equal(ChannelState.Idle, channel1.State);

            var reply11 = client1.Authentication(new AuthRequest { AccountName = "Test1", AccountPsw = string.Empty });

            Assert.True(reply11.RequestState);
            Assert.Equal(10000, reply11.SessionId);

            var reply12 = client1.GameQueue(
                new QueueRequest
                {
                    GameType = GameType.Normal,
                    DeckType = DeckType.Random,
                    DeckData = string.Empty
                },
                new Metadata {
                    new Metadata.Entry("token", reply11.SessionToken)
                });

            Assert.True(reply12.RequestState);


            var channel2 = new Channel(_target, ChannelCredentials.Insecure);
            Assert.Equal(ChannelState.Idle, channel2.State);

            var client2 = new GameServerService.GameServerServiceClient(channel2);
            Assert.Equal(ChannelState.Idle, channel2.State);

            var reply21 = client2.Authentication(new AuthRequest { AccountName = "Test2", AccountPsw = string.Empty });

            Assert.True(reply21.RequestState);
            Assert.Equal(10001, reply21.SessionId);

            var reply22 = client2.GameQueue(
                new QueueRequest
                {
                    GameType = GameType.Normal,
                    DeckType = DeckType.Random,
                    DeckData = string.Empty
                },
                new Metadata {
                    new Metadata.Entry("token", reply21.SessionToken)
                });

            Assert.True(reply22.RequestState);

            var matchMaker = _server.GetMatchMakerService();

            Assert.Equal(0, matchMaker.MatchGames.Count); 

            matchMaker.Start(1);

            Thread.Sleep(2000);

            Assert.Equal(1, matchMaker.MatchGames.Count);

            _server.Stop();
        }

    }
}
