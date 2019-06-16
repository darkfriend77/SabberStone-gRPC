using Grpc.Core;
using log4net;
using SabberStoneClient.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using static GameServerService;

namespace SabberStoneClient
{
    public class GameClient
    {
        private static readonly ILog Log = Logger.Instance.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private int _port = 50052;

        private string _target;

        private Channel _channel;

        private GameServerServiceClient _client;

        private GameClientState _gameClientState;

        private int _sessionId;

        private string _sessionToken;

        public GameClient()
        {
            _target = $"127.0.0.1:{_port}";
            _gameClientState = GameClientState.None;
        }

        public void Connect()
        {
            _channel = new Channel(_target, ChannelCredentials.Insecure);
            _client = new GameServerServiceClient(_channel);
            _gameClientState = GameClientState.Connected;
        }

        public void Register(string accountName, string accountPsw)
        {
            if (_gameClientState != GameClientState.Connected)
            {
                Log.Warn("Client isn't connected.");
                return;
            }

            var authReply = _client.Authentication(new AuthRequest { AccountName = accountName, AccountPsw = accountPsw });

            if (!authReply.RequestState)
            {
                Log.Warn("Bad RegisterRequest.");
                return;
            }

            _sessionId = authReply.SessionId;
            _sessionToken = authReply.SessionToken;




            _gameClientState = GameClientState.Registred;
        }

        public void Queue(GameType gameType = GameType.Normal, DeckType deckType = DeckType.Random, string deckData = null)
        {
            if (_gameClientState != GameClientState.Registred)
            {
                Log.Warn("Client isn't registred.");
                return;
            }

            var queueReply = _client.GameQueue(
                new QueueRequest
                {
                    GameType = gameType,
                    DeckType = deckType,
                    DeckData = deckData ?? string.Empty
                },
                new Metadata {
                    new Metadata.Entry("token", _sessionToken)
            });

            if (!queueReply.RequestState)
            {
                Log.Warn("Bad QueueRequest.");
                return;
            }

            _gameClientState = GameClientState.Queued;
        }

        public void Test()
        {
            var channel1 = new Channel(_target, ChannelCredentials.Insecure);

            var client1 = new GameServerService.GameServerServiceClient(channel1);
            var reply11 = client1.Authentication(new AuthRequest { AccountName = "Test1", AccountPsw = string.Empty });


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



        }
    }
}
