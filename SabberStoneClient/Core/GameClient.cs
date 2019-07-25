using Grpc.Core;
using Newtonsoft.Json;
using SabberStoneClient.Core;
using SabberStoneContract.Model;
using SabberStoneCore.Kettle;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static GameServerService;

namespace SabberStoneClient
{
    public class GameClient
    {
        //private static readonly ILog Log = Logger.Instance.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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

        private bool _isBot;

        private Random _random;

        public ConcurrentQueue<IPowerHistoryEntry> HistoryEntries { get; }

        public PowerChoices PowerChoices { get; private set; }

        public List<PowerOption> PowerOptionList { get; private set; }

        public GameClient(int port, bool isBot = false)
        {
            _port = port;
            _isBot = isBot;
            _random = new Random();

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

            //Log.Info($"Register done.");

        }

        public async void GameServerChannel()
        {
            using (var call = _client.GameServerChannel(headers: new Metadata { new Metadata.Entry("token", _sessionToken) }))
            {
                // listen to game server
                var response = Task.Run(async () =>
                {
                    while (await call.ResponseStream.MoveNext(CancellationToken.None) &&_gameClientState != GameClientState.None)
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
                //Log.Warn($"There is no write stream currently.");
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
            //Log.Info($"SetClientState {gameClientState}");
            _gameClientState = gameClientState;
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
                case MsgType.Initialisation:
                    SetClientState(GameClientState.Registred);
                    break;

                case MsgType.Invitation:
                    _gameId = gameData.GameId;
                    _playerId = gameData.PlayerId;
                    if (_isBot)
                    {
                        WriteGameData(MsgType.Invitation, true, new GameData() { GameId = _gameId, PlayerId = _playerId, GameDataType = GameDataType.None });
                    }
                    break;

                case MsgType.InGame:
                    switch (gameData.GameDataType)
                    {
                        case GameDataType.None:
                            SetClientState(GameClientState.InGame);
                            break;

                        case GameDataType.PowerHistory:
                            List<IPowerHistoryEntry> powerHistoryEntries = JsonConvert.DeserializeObject<List<IPowerHistoryEntry>>(gameData.GameDataObject, new PowerHistoryConverter());
                            powerHistoryEntries.ForEach(p => HistoryEntries.Enqueue(p));
                            break;

                        case GameDataType.PowerChoices:
                            PowerChoices = JsonConvert.DeserializeObject<PowerChoices>(gameData.GameDataObject);
                            if (_isBot)
                            {
                                var powerChoicesId = _random.Next(PowerChoices.Entities.Count);
                                var powerChoicesChoice = JsonConvert.SerializeObject(new PowerChoices() { ChoiceType = PowerChoices.ChoiceType, Entities = new List<int>() { PowerChoices.Entities[powerChoicesId] } });
                                WriteGameData(MsgType.InGame, true, new GameData() { GameId = _gameId, PlayerId = _playerId, GameDataType = GameDataType.PowerChoices, GameDataObject = powerChoicesChoice });
                                //Log.Info($"choices:{powerChoicesId}");
                                PowerChoices = null;
                            }
                            break;

                        case GameDataType.PowerOptions:
                            var powerOptions = JsonConvert.DeserializeObject<PowerOptions>(gameData.GameDataObject);
                            if (powerOptions.PowerOptionList != null &&
                                powerOptions.PowerOptionList.Count > 0)
                            {
                                PowerOptionList = powerOptions.PowerOptionList;
                                if (_isBot)
                                {
                                    var powerOptionId = _random.Next(PowerOptionList.Count);
                                    var powerOption = PowerOptionList.ElementAt(powerOptionId);
                                    var target = powerOption.MainOption?.Targets != null && powerOption.MainOption.Targets.Count > 0
                                        ? powerOption.MainOption.Targets.ElementAt(_random.Next(powerOption.MainOption.Targets.Count))
                                        : 0;
                                    var subOption = powerOption.SubOptions != null && powerOption.SubOptions.Count > 0
                                        ? _random.Next(powerOption.SubOptions.Count)
                                        : 0;
                                    var powerOptionChoice = JsonConvert.SerializeObject(new PowerOptionChoice() { PowerOption = powerOption, Target = target, Position = 0, SubOption = subOption });
                                    WriteGameData(MsgType.InGame, true, new GameData() { GameId = _gameId, PlayerId = _playerId, GameDataType = GameDataType.PowerOptions, GameDataObject = powerOptionChoice });
                                    //Log.Info($"target:{target}, position:0, suboption: {subOption} {powerOption.Print()}");
                                    PowerOptionList.Clear();
                                }
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

            SetClientState(GameClientState.Queued);
        }

    }
}
