using Grpc.Core;
//using log4net;
using Newtonsoft.Json;
using SabberStoneContract.Client;
using SabberStoneContract.Helper;
using SabberStoneContract.Model;
using SabberStoneCore.Kettle;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using static GameServerService;

namespace SabberStoneContract.Core
{
    public class GameClient
    {
        //private static readonly ILog Log = Logger.Instance.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly int _port;

        private readonly string _target;

        private Channel _channel;

        private GameServerServiceClient _client;

        private GameClientState _gameClientState;

        private int _sessionId;

        private string _sessionToken;

        private readonly CancellationTokenSource _cancellationTokenSource;

        private ConcurrentQueue<GameServerStream> _gameServerStreamQueue;

        private GameController _gameController;

        public GameClientState GameClientState
        {
            get => _gameClientState;
            private set
            {
                var oldValue = _gameClientState;
                _gameClientState = value;
                CallGameClientState(oldValue, value);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="port"></param>
        /// <param name="sabberStoneAI"></param>
        public GameClient(string targetIp, int port, GameController gameController)
        {
            _port = port;
            _target = $"{targetIp}:{_port}";

            _cancellationTokenSource = new CancellationTokenSource();

            _gameServerStreamQueue = new ConcurrentQueue<GameServerStream>();

            _gameController = gameController;
            _gameController.SetSendGameMessage(SendGameMessage);

            GameClientState = GameClientState.None;
        }

        public void Connect()
        {
            _channel = new Channel(_target, ChannelCredentials.Insecure);
            _client = new GameServerServiceClient(_channel);
            GameClientState = GameClientState.Connected;
        }

        public void Register(string accountName, string accountPsw)
        {
            if (GameClientState != GameClientState.Connected)
            {
                //Log.Warn("Client isn't connected.");
                return;
            }

            var authReply = _client.Authentication(new AuthRequest { AccountName = accountName, AccountPsw = accountPsw });

            if (!authReply.RequestState)
            {
                //Log.Warn("Bad RegisterRequest.");
                return;
            }

            _sessionId = authReply.SessionId;
            _sessionToken = authReply.SessionToken;

            GameServerChannel();

            GameClientState = GameClientState.Registred;
        }

        public void MatchGame()
        {
            if (GameClientState != GameClientState.InGame)
            {
                //Log.Warn("Client isn't in a game.");
                return;
            }

            var matchGameReply = _client.MatchGame(new MatchGameRequest { GameId = _gameController.GameId }, new Metadata { new Metadata.Entry("token", _sessionToken) });

            if (!matchGameReply.RequestState)
            {
                //Log.Warn("Bad MatchGameRequest.");
                return;
            }
        }

        public async void GameServerChannel()
        {
            using (var call = _client.GameServerChannel(headers: new Metadata { new Metadata.Entry("token", _sessionToken) }, cancellationToken: _cancellationTokenSource.Token))
            {
                var requestStreamWriterTask = new Task(async () =>
                {
                    while (!_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        if (_gameServerStreamQueue.TryDequeue(out GameServerStream gameServerStream))
                        {
                            await call.RequestStream.WriteAsync(gameServerStream);
                        }
                        else
                        {
                            Thread.Sleep(1);
                        }
                    }
                });
                requestStreamWriterTask.Start();

                await Task.Run(async () =>
                {
                    try
                    {
                        while (await call.ResponseStream.MoveNext(_cancellationTokenSource.Token))
                        {
                            ProcessChannelMessage(call.ResponseStream.Current);
                        }
                    }
                    catch (RpcException exception)
                    {
                        if (exception.StatusCode != StatusCode.Cancelled)
                        {
                            //Log.Error(exception.ToString());
                            throw exception;
                        }
                    }
                });
            }
        }

        public void SendGameMessage(MsgType msgType, bool msgState, string msgData)
        {
            _gameServerStreamQueue.Enqueue(new GameServerStream
            {
                MessageType = msgType,
                MessageState = msgState,
                Message = msgData
            });
        }

        public void Disconnect()
        {
            try
            {
                var serverReply = _client.Disconnect(new ServerRequest(), new Metadata { new Metadata.Entry("token", _sessionToken ?? string.Empty) }, DateTime.UtcNow.AddSeconds(5));
            }
            catch (RpcException exception)
            {
                if (exception.StatusCode != StatusCode.Unavailable)
                {
                    //Log.Error(exception.ToString());
                    throw exception;
                }
            }
            _cancellationTokenSource.Cancel();

            _channel.ShutdownAsync().Wait();

            GameClientState = GameClientState.None;
        }

        private void ProcessChannelMessage(GameServerStream current)
        {
            if (!current.MessageState)
            {
                //Log.Warn($"Failed messageType {current.MessageType}, '{current.Message}'!");
                return;
            }

            GameData gameData = null;
            if (current.Message != string.Empty)
            {
                gameData = JsonConvert.DeserializeObject<GameData>(current.Message);
                //Log.Info($"GameData[Id:{gameData.GameId},Player:{gameData.PlayerId}]: {gameData.GameDataType} received");
            }
            else
            {
                //Log.Info($"Message[{current.MessageState},{current.MessageType}]: received.");
            }

            switch (current.MessageType)
            {
                //                case MsgType.Initialisation:
                //                    break;

                case MsgType.Invitation:

                    // preparing for a new game
                    _gameController.Reset();

                    _gameController.GameId = gameData.GameId;
                    _gameController.PlayerId = gameData.PlayerId;
                    GameClientState = GameClientState.Invited;

                    // action call here
                    CallInvitation();
                    break;

                case MsgType.InGame:
                    switch (gameData.GameDataType)
                    {
                        case GameDataType.Initialisation:
                            _gameController.SetUserInfos(JsonConvert.DeserializeObject<List<UserInfo>>(gameData.GameDataObject));
                            GameClientState = GameClientState.InGame;
                            break;

                        case GameDataType.PowerHistory:
                            _gameController.SetPowerHistory(JsonConvert.DeserializeObject<List<IPowerHistoryEntry>>(gameData.GameDataObject, new PowerHistoryConverter()));
                            break;

                        case GameDataType.PowerChoices:
                            _gameController.SetPowerChoices(JsonConvert.DeserializeObject<PowerChoices>(gameData.GameDataObject));

                            break;

                        case GameDataType.PowerOptions:
                            _gameController.SetPowerOptions(JsonConvert.DeserializeObject<PowerOptions>(gameData.GameDataObject));
                            break;

                        case GameDataType.Result:
                            GameClientState = GameClientState.Registred;
                            break;
                    }
                    break;
            }
        }

        public void Queue(GameType gameType = GameType.Normal, DeckType deckType = DeckType.Random, string deckData = null)
        {
            if (GameClientState != GameClientState.Registred)
            {
                //Log.Warn("Client isn't registred.");
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
                //Log.Warn("Bad QueueRequest.");
                return;
            }

            GameClientState = GameClientState.Queued;
        }

        public async virtual void CallGameClientState(GameClientState oldState, GameClientState newState)
        {
            await Task.Run(() =>
            {
                //Log.Info($"GameClientStateChange: {oldState} -> {newState}");
            });
        }

        public async virtual void CallInvitation()
        {
            await Task.Run(() =>
            {
                _gameController.SendInvitationReply(true);
            });
        }

    }
}
