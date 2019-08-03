using Grpc.Core;
using log4net;
using Newtonsoft.Json;
using SabberStoneClient.AI;
using SabberStoneClient.Core;
using SabberStoneContract.Model;
using SabberStoneCore.Kettle;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        public GameClientState GameClientState { get { return _gameClientState; } }

        private IClientStreamWriter<GameServerStream> _writeStream;

        private int _sessionId;

        private string _sessionToken;

        private int _gameId;

        private int _playerId;

        private Random _random;

        private List<UserInfo> _userInfos;

        public UserInfo MyUserInfo => _userInfos.Where(p => p.PlayerId == _playerId).FirstOrDefault();

        public UserInfo OpUserInfo => _userInfos.Where(p => p.PlayerId != _playerId).FirstOrDefault();

        public ConcurrentQueue<IPowerHistoryEntry> HistoryEntries { get; }

        public PowerChoices PowerChoices { get; private set; }

        public List<PowerOption> PowerOptionList { get; private set; }

        private ISabberStoneAI _sabberStoneAI;

        public GameClient(int port, ISabberStoneAI sabberStoneAI)
        {
            _port = port;
            _sabberStoneAI = sabberStoneAI ?? new RandomAI();

            _target = $"127.0.0.1:{_port}";
            SetClientState(_gameClientState = GameClientState.None);

            _gameId = -1;
            _playerId = -1;
            _userInfos = new List<UserInfo>();

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

        public void MatchGame()
        {
            if (_gameClientState != GameClientState.InGame)
            {
                Log.Warn("Client isn't in a game.");
                return;
            }

            var matchGameReply = _client.MatchGame(new MatchGameRequest { GameId = _gameId }, new Metadata { new Metadata.Entry("token", _sessionToken) });

            if (!matchGameReply.RequestState)
            {
                Log.Warn("Bad MatchGameRequest.");
                return;
            }

            // TODO do something with the game object ...

            Log.Info($"Got match game successfully.");
        }

        public async void GameServerChannel()
        {
            using (var call = _client.GameServerChannel(headers: new Metadata { new Metadata.Entry("token", _sessionToken) }))
            {
                // listen to game server
                var response = Task.Run(async () =>
                {
                    while (await call.ResponseStream.MoveNext(CancellationToken.None) && _gameClientState != GameClientState.None)
                    {
                        ProcessChannelMessage(call.ResponseStream.Current);
                    };
                });

                _writeStream = call.RequestStream;
                WriteGameServerStream(MsgType.Initialisation, true, string.Empty);

                await response;
            }
        }

        public async void WriteGameServerStream(MsgType messageType, bool messageState, string message)
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

        public void WriteGameData(MsgType messageType, bool messageState, GameData gameData)
        {
            WriteGameServerStream(messageType, messageState, JsonConvert.SerializeObject(gameData));
        }

        public void SendPowerChoicesChoice(PowerChoices powerChoices)
        {
            WriteGameData(MsgType.InGame, true, new GameData() { GameId = _gameId, PlayerId = _playerId, GameDataType = GameDataType.PowerChoices, GameDataObject = JsonConvert.SerializeObject(powerChoices) });
            PowerChoices = null;
        }

        public void SendPowerOptionChoice(PowerOptionChoice powerOptionChoice)
        {
            WriteGameData(MsgType.InGame, true, new GameData() { GameId = _gameId, PlayerId = _playerId, GameDataType = GameDataType.PowerOptions, GameDataObject = JsonConvert.SerializeObject(powerOptionChoice) });
            PowerOptionList.Clear();
        }

        public async void Disconnect()
        {
            if (_writeStream != null)
            {
                await _writeStream.CompleteAsync();
            }

            SetClientState(GameClientState.None);

            await _channel.ShutdownAsync();
        }

        private void SetClientState(GameClientState gameClientState)
        {
            Log.Info($"SetClientState {gameClientState}");
            _gameClientState = gameClientState;
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
                    SetClientState(GameClientState.Registred);
                    break;

                case MsgType.Invitation:
                    _gameId = gameData.GameId;
                    _playerId = gameData.PlayerId;

                    // action call here
                    ActionCallInvitation();
                    break;

                case MsgType.InGame:
                    switch (gameData.GameDataType)
                    {
                        case GameDataType.Initialisation:
                            _userInfos = JsonConvert.DeserializeObject<List<UserInfo>>(gameData.GameDataObject);
                            SetClientState(GameClientState.InGame);
                            Log.Info($"Initialized game against account {OpUserInfo.AccountName}!");

                            // action call here
                            ActionCallInitialisation();
                            break;

                        case GameDataType.PowerHistory:
                            List<IPowerHistoryEntry> powerHistoryEntries = JsonConvert.DeserializeObject<List<IPowerHistoryEntry>>(gameData.GameDataObject, new PowerHistoryConverter());
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
                            SetClientState(GameClientState.Registred);
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


        public async virtual void ActionCallInvitation()
        {
            await Task.Run(() =>
            {
                WriteGameData(MsgType.Invitation, true, new GameData() { GameId = _gameId, PlayerId = _playerId, GameDataType = GameDataType.None });
            });
        }

        public async virtual void ActionCallInitialisation()
        {
            await Task.Run(() =>
            {
                MatchGame();
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
