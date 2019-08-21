using Grpc.Core;
using log4net;
using Newtonsoft.Json;
using SabberStoneClient.Interface;
using SabberStoneContract.Model;
using SabberStoneCore.Kettle;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using static GameServerService;

namespace SabberStoneClient.Core
{
    public class GameClient
    {
        private static readonly ILog Log = Logger.Instance.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private int _port;

        private string _target;

        private Channel _channel;

        private GameServerServiceClient _client;

        private GameClientState _gameClientState;

        public GameClientState GameClientState
        {
            get => _gameClientState;
            private set
            {
                var oldValue = _gameClientState;

                _gameClientState = value;
                StateChanged?.Invoke(this, value);
                ActionGameClientStateChange(oldValue, value);

                if (oldValue == GameClientState.InGame && value != GameClientState.InGame)
                {
                    // game left clean up
                    AfterInGame();
                }

            }
        }

        private IClientStreamWriter<GameServerStream> _writeStream;

        private int _sessionId;

        private string _sessionToken;

        private int _gameId;

        private int _playerId;

        private bool _logGames;

        private List<UserInfo> _userInfos;

        private TaskCompletionSource<object> registerWaiter;

        public event Action<GameClient, GameClientState> StateChanged;

        public string AccountName { get; private set; }

        public UserInfo MyUserInfo => _userInfos.FirstOrDefault(p => p.PlayerId == _playerId);

        public UserInfo OpUserInfo => _userInfos.FirstOrDefault(p => p.PlayerId != _playerId);

        private List<string> _fullGameHistory { get; }

        public ConcurrentQueue<IPowerHistoryEntry> HistoryEntries { get; }

        public PowerChoices PowerChoices { get; private set; }

        public List<PowerOption> PowerOptionList { get; private set; }

        private IGameAI _sabberStoneAI;

        public GameClient(int port, IGameAI sabberStoneAI, string accountName = "", bool logGames = false)
        {
            _port = port;
            _sabberStoneAI = sabberStoneAI ?? new RandomAI();

            _target = $"127.0.0.1:{_port}";
            GameClientState = GameClientState.None;

            _gameId = -1;
            _playerId = -1;
            _logGames = logGames;

            _userInfos = new List<UserInfo>();

            AccountName = accountName;
            _fullGameHistory = new List<string>();
            HistoryEntries = new ConcurrentQueue<IPowerHistoryEntry>();
            PowerOptionList = new List<PowerOption>();
        }

        public void Connect()
        {
            _channel = new Channel(_target, ChannelCredentials.Insecure);
            _client = new GameServerServiceClient(_channel);
            GameClientState = GameClientState.Connected;
        }

        public async Task Register(string accountName, string accountPsw)
        {
            if (GameClientState != GameClientState.Connected)
            {
                Log.Warn("Client isn't connected.");
                return;
            }

            var authReply = _client.Authentication(new AuthRequest { AccountName = accountName, AccountPsw = accountPsw });
            if (!authReply.ServerReply.RequestState)
            {
                Log.Warn(authReply.ServerReply.RequestMessage);
                return;
            }

            _sessionId = authReply.SessionId;
            _sessionToken = authReply.SessionToken;

            GameServerChannel();

            GameStateChannel();

            registerWaiter = new TaskCompletionSource<object>();
            await registerWaiter.Task;

            Log.Info($"Register done.");
        }

        public void MatchGame()
        {
            if (GameClientState != GameClientState.InGame)
            {
                Log.Warn("Client isn't in a game.");
                return;
            }

            var matchGameReply = _client.MatchGame(new MatchGameRequest { GameId = _gameId }, new Metadata { new Metadata.Entry("token", _sessionToken) });

            if (!matchGameReply.ServerReply.RequestState)
            {
                Log.Warn("Bad MatchGameRequest.");
                return;
            }

            // TODO do something with the game object ...
            Log.Info($"Got match game successfully.");
        }

        public async void GameStateChannel()
        {
            using (var call = _client.GameStateChannel(new GameStateStreamRequest(), new Metadata { new Metadata.Entry("token", _sessionToken) }))
            {
                var responseStream = call.ResponseStream;
                // listen to game server
                var response = Task.Run(async () => {
                    while (await responseStream.MoveNext(CancellationToken.None))
                    {
                        Log.Info(responseStream.Current.Message);
                    }

                    Log.Info("### GameStateChannel got terminated!!! ###");
                });

                await response;
            }
        }

        public async void GameServerChannel()
        {
            using (var call = _client.GameServerChannel(headers: new Metadata { new Metadata.Entry("token", _sessionToken) }))
            {
                // listen to game server
                var response = Task.Run(async () =>
                {
                    while (await call.ResponseStream.MoveNext(CancellationToken.None) && GameClientState != GameClientState.None)
                    {
                        try
                        {
                            ProcessChannelMessage(call.ResponseStream.Current);
                        }
                        catch
                        {
                            ;
                        }

                    };
                });

                _writeStream = call.RequestStream;
                await WriteGameServerStream(MsgType.Initialisation, true, string.Empty);

                //await call.RequestStream.CompleteAsync();
                await response;
            }
        }

        public async Task WriteGameServerStream(MsgType messageType, bool messageState, string message)
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

            Log.Debug($"{AccountName} sent [{messageType}]");
        }

        public void WriteGameData(MsgType messageType, bool messageState, GameData gameData)
        {
            // waiting on sending before going on ...
            WriteGameServerStream(messageType, messageState, JsonConvert.SerializeObject(gameData)).Wait();
        }

        public void SendInvitationReply(bool accept)
        {
            WriteGameData(MsgType.Invitation, accept, new GameData { GameId = _gameId, PlayerId = _playerId, GameDataType = GameDataType.None });
        }

        public void SendPowerChoicesChoice(PowerChoices powerChoices)
        {
            // clear before sent ...
            PowerChoices = null;
            WriteGameData(MsgType.InGame, true, new GameData() { GameId = _gameId, PlayerId = _playerId, GameDataType = GameDataType.PowerChoices, GameDataObject = JsonConvert.SerializeObject(powerChoices) });
        }

        public void SendPowerOptionChoice(PowerOptionChoice powerOptionChoice)
        {
            // clear before sent ...
            PowerOptionList.Clear();
            WriteGameData(MsgType.InGame, true, new GameData() { GameId = _gameId, PlayerId = _playerId, GameDataType = GameDataType.PowerOptions, GameDataObject = JsonConvert.SerializeObject(powerOptionChoice) });
        }

        public async void Disconnect()
        {
            if (_writeStream != null)
            {
                await _writeStream.CompleteAsync();
            }

            GameClientState = GameClientState.None;

            await _channel.ShutdownAsync();
        }

        private void ProcessChannelMessage(GameServerStream current)
        {
            if (!current.MessageState)
            {
                Log.Warn($"Failed messageType {current.MessageType}, '{current.Message}'!");
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
                case MsgType.Initialisation:
                    GameClientState = GameClientState.Registred;
                    registerWaiter.SetResult(new object());
                    break;

                case MsgType.Invitation:
                    _gameId = gameData.GameId;
                    _playerId = gameData.PlayerId;
                    GameClientState = GameClientState.Invited;

                    // action call here
                    ActionCallInvitation();
                    break;

                case MsgType.InGame:
                    switch (gameData.GameDataType)
                    {
                        case GameDataType.Initialisation:
                            _userInfos = JsonConvert.DeserializeObject<List<UserInfo>>(gameData.GameDataObject);
                            GameClientState = GameClientState.InGame;
                            Log.Info($"Initialized game against account {OpUserInfo.AccountName}!");

                            // action call here
                            ActionCallInitialisation();
                            break;

                        case GameDataType.PowerHistory:
                            List<IPowerHistoryEntry> powerHistoryEntries = JsonConvert.DeserializeObject<List<IPowerHistoryEntry>>(gameData.GameDataObject, new PowerHistoryConverter());
                            _fullGameHistory.Add(gameData.GameDataObject);
                            powerHistoryEntries.ForEach(p => HistoryEntries.Enqueue(p));
                            break;

                        case GameDataType.PowerChoices:
                            PowerChoices = JsonConvert.DeserializeObject<PowerChoices>(gameData.GameDataObject);

                            // action call here
                            ActionCallPowerChoices();
                            break;

                        case GameDataType.PowerOptions:
                            var powerOptions = JsonConvert.DeserializeObject<PowerOptions>(gameData.GameDataObject);
                            PowerOptionList = powerOptions.PowerOptionList;
                            if (PowerOptionList != null &&
                               PowerOptionList.Count > 0)
                            {

                                // action call here
                                ActionCallPowerOptions();
                                break;
                            }
                            break;

                        case GameDataType.Result:

                            //Log.Info($" ... ");
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

            GameClientState = GameClientState.Queued;
        }

        private void AfterInGame()
        {
            if (_logGames && _fullGameHistory.Any())
            {
                var gameLogFilename = $"GAME-{MyUserInfo.GameId}-{MyUserInfo.PlayerId}-{DateTime.Now.Ticks}.log";
                var gameLogFilePath = Path.Combine(Directory.GetCurrentDirectory(), gameLogFilename);
                Log.Info($"Writing game log to {gameLogFilePath}.");
                File.WriteAllText(gameLogFilePath, JsonConvert.SerializeObject(_fullGameHistory));
            }

            // clean up
            _userInfos.Clear();
            _fullGameHistory.Clear();
            while (!HistoryEntries.IsEmpty)
            {
                HistoryEntries.TryDequeue(out _);
            }
            PowerChoices = null;
            PowerOptionList.Clear();
        }

        public async virtual void ActionGameClientStateChange(GameClientState oldState, GameClientState newState)
        {
            await Task.Run(() =>
            {
                Log.Info($"GameClientStateChange: {oldState} -> {newState}");
            });
        }

        public async virtual void ActionCallInvitation()
        {
            await Task.Run(() =>
            {
                SendInvitationReply(true);
            });
        }

        public async virtual void ActionCallInitialisation()
        {
            await Task.Run(() =>
            {
                //MatchGame();
            });
        }

        public async virtual void ActionCallPowerChoices()
        {
            await Task.Run(() =>
            {
                SendPowerChoicesChoice(_sabberStoneAI.PowerChoices(PowerChoices));
            });
        }

        public async virtual void ActionCallPowerOptions()
        {
            await Task.Run(() =>
            {
                SendPowerOptionChoice(_sabberStoneAI.PowerOptions(PowerOptionList));
            });
        }
    }
}
