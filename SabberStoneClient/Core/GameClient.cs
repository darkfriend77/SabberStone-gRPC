﻿using Grpc.Core;
using log4net;
using Newtonsoft.Json;
using SabberStoneClient.Core;
using SabberStoneContract.Model;
using SabberStoneCore.Kettle;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static GameServerService;

namespace SabberStoneClient
{
    public class GameClient
    {
        private static readonly ILog Log = Logger.Instance.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private int _port;

        private string _target;

        private Channel _channel;

        private GameServerServiceClient _client;

        private GameClientState _gameClientState;

        public GameClientState GameClientState { get { return _gameClientState; } }

        private IClientStreamWriter<GameServerStream> _writeStream;

        private int _sessionId;

        private string _sessionToken;

        private int _gameId;

        private int _playerId;

        public ConcurrentQueue<IPowerHistoryEntry> HistoryEntries { get; }

        public List<PowerOption> PowerOptionList { get; private set; }

        public GameClient(int port)
        {
            _port = port;
            _target = $"127.0.0.1:{_port}";
            SetClientState(_gameClientState = GameClientState.None);

            _gameId = -1;
            _playerId = -1;

            HistoryEntries = new ConcurrentQueue<IPowerHistoryEntry>();
            PowerOptionList = new List<PowerOption>();
        }

        public void Connect()
        {
            _channel = new Channel(_target, ChannelCredentials.Insecure);
            _client = new GameServerServiceClient(_channel);
            SetClientState(GameClientState.Connected);
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

            GameServerChannel();

            Log.Info($"Register done.");

        }

        public async void GameServerChannel()
        {
            using (var call = _client.GameServerChannel(headers: new Metadata { new Metadata.Entry("token", _sessionToken) }))
            {
                // listen to game server
                var response = Task.Run(async () =>
                {
                    while (await call.ResponseStream.MoveNext(CancellationToken.None))
                    {
                        ProcessChannelMessage(call.ResponseStream.Current);
                    };
                });

                _writeStream = call.RequestStream;
                WriteGameServerStream(MessageType.Initialisation, true, string.Empty);

                await response;
            }
        }

        public async void WriteGameServerStream(MessageType messageType, bool messageState, string message)
        {
            if (_writeStream == null)
            {
                Log.Warn($"There is no write stream currently.");
                return;
            }

            await _writeStream.WriteAsync(new GameServerStream
            {
                MessageType = messageType,
                MessageState = messageState,
                Message = message
            });
        }

        public void WriteGameData(MessageType messageType, bool messageState, GameData gameData)
        {
            WriteGameServerStream(messageType, messageState, JsonConvert.SerializeObject(gameData));
        }

        public Task Disconnect()
        {
            throw new NotImplementedException();
        }

        private void SetClientState(GameClientState gameClientState)
        {
            Log.Info($"SetClientState {gameClientState}");
            _gameClientState = gameClientState;
        }

        private void ProcessChannelMessage(GameServerStream current)
        {
            Log.Warn($"ProcessChannelMessage[{current.MessageState},{current.MessageType}]: '{(current.Message.Length > 100 ? current.Message.Substring(0, 100) + "..." : current.Message)}'");

            if (!current.MessageState)
            {
                Log.Warn($"Failed messageType {current.MessageType}, '{current.Message}'!");
                return;
            }
            GameData gameData = null;
            switch (current.MessageType)
            {
                case MessageType.Initialisation:
                    SetClientState(GameClientState.Registred);
                    break;

                case MessageType.Invitation:
                    gameData = JsonConvert.DeserializeObject<GameData>(current.Message);
                    _gameId = gameData.GameId;
                    _playerId = gameData.PlayerId;

                    WriteGameData(MessageType.Invitation, true, new GameData() { GameId = _gameId, PlayerId = _playerId, GameDataType = GameDataType.None });
                    break;

                case MessageType.InGame:
                    gameData = JsonConvert.DeserializeObject<GameData>(current.Message);
                    switch (gameData.GameDataType)
                    {
                        case GameDataType.None:
                            SetClientState(GameClientState.InGame);
                            break;

                        case GameDataType.PowerHistory:
                            List<IPowerHistoryEntry> powerHistoryEntries = JsonConvert.DeserializeObject<List<IPowerHistoryEntry>>(gameData.GameDataObject, new PowerHistoryConverter());
                            powerHistoryEntries.ForEach(p => HistoryEntries.Enqueue(p));
                            break;
                    }
                    break;
            }
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

            SetClientState(GameClientState.Queued);
        }

    }
}
