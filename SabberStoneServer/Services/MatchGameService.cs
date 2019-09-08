using log4net;
using Newtonsoft.Json;
using SabberStoneContract.Helper;
using SabberStoneContract.Model;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneServer.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using SabberStoneContract.Core;

namespace SabberStoneServer.Services
{
    public partial class MatchGameService
    {
        private static readonly ILog Log = Logger.Instance.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public int GameId { get; }

        private readonly GameServerServiceImpl _gameServerService;

        private readonly Random _random;

        public UserClient Player1 { get; }

        public PlayState Play1State => _game.Player1.PlayState;

        public UserClient Player2 { get; }

        public PlayState Play2State => _game.Player2.PlayState;

        private Game _game;

        public MatchGame MatchGame(int playerId) => CreateMatchGameReply();

        private readonly int _id;

        private readonly string _token;

        private UserClient UserById(int id) => Player1.PlayerId == id ? Player1 : Player2.PlayerId == id ? Player2 : null;

        public bool IsFinished => Player1.PlayerState == PlayerState.Quit && Player2.PlayerState == PlayerState.Quit;

        public MatchGameService(GameServerServiceImpl gameServerService, int index, UserClient player1, UserClient player2)
        {
            _gameServerService = gameServerService;
            _random = new Random();
            GameId = index;

            Player1 = player1;
            Player1.GameId = GameId;
            Player1.PlayerId = 1;
            Player1.PlayerState = PlayerState.None;

            Player2 = player2;
            Player2.GameId = GameId;
            Player2.PlayerId = 2;
            Player2.PlayerState = PlayerState.None;

            _id = 2;
            _token = $"matchgame{GameId}";

            _game = null;
        }

        public void Initialize()
        {
            // game invitation request for player 1
            Player1.PlayerState = PlayerState.Invitation;
            SendGameData(Player1, MsgType.Invitation, true, GameDataType.None);

            // game invitation request for player 2
            Player2.PlayerState = PlayerState.Invitation;
            SendGameData(Player2, MsgType.Invitation, true, GameDataType.None);
        }

        public void Start(GameConfigInfo gameConfigInfo)
        {
            Log.Info($"[_gameId:{GameId}] Game creation is happening in a few seconds!!!");

            _game = SabberStoneConverter.CreateGame(Player1, Player2, gameConfigInfo);

            Log.Info($"[_gameId:{GameId}] Game creation done!");
            _game.StartGame();
        }

        internal void InvitationReply(bool state, GameData gameData)
        {
            if (!state)
            {
                Stop();
                return;
            }

            var userInfoData = UserById(gameData.PlayerId);
            userInfoData.UserState = UserState.InGame;

            if (Player1.UserState == UserState.InGame && Player2.UserState == UserState.InGame)
            {
                GameConfigInfo gameConfigInfo = new GameConfigInfo()
                {
                    SkipMulligan = true,
                    Shuffle = true,
                    FillDecks = true,
                    Logging = true,
                    History = true,
                    RandomSeed = _random.Next()
                };

                Start(gameConfigInfo);

                Player1.GameConfigInfo = gameConfigInfo;
                Player2.GameConfigInfo = gameConfigInfo;

                // we send over opponend user info which is reduced to open available informations
                SendGameData(Player1, MsgType.InGame, true, GameDataType.Initialisation,
                    JsonConvert.SerializeObject(new List<UserInfo> { Player1, Player2 }));

                SendGameData(Player2, MsgType.InGame, true, GameDataType.Initialisation,
                    JsonConvert.SerializeObject(new List<UserInfo> { Player1, Player2 }));

                SendHistoryToPlayers();
                SendOptionsOrChoicesToPlayers();
            }
        }

        internal void ProcessGameData(GameData gameData)
        {
            var userInfoData = UserById(gameData.PlayerId);

            if (userInfoData == null)
            {
                Stop();
                return;
            }

            switch (gameData.GameDataType)
            {
                case GameDataType.PowerOption:
                    // 1. Deserialise the given GameData == Option
                    PowerOptionChoice powerOptionChoice = JsonConvert.DeserializeObject<PowerOptionChoice>(gameData.GameDataObject);
                    // 2. Convert it to the SabberStone object
                    PlayerTask optionTask = SabberStoneConverter.CreatePlayerTaskOption(_game, powerOptionChoice.PowerOption, powerOptionChoice.Target, powerOptionChoice.Position, powerOptionChoice.SubOption);
                    // 3. Run SabberStoneCore
                    _game.Process(optionTask);

                    //if (powerOptionChoice.PowerOption.OptionType == OptionType.END_TURN)
                    //{
                    //    Log.Info($"State[{_game.State}]-T[{_game.Turn}] Hero1: {_game.Player1.Hero.Health} HP // Hero2: {_game.Player2.Hero.Health} HP");
                    //}

                    // 4. Broadcast the processed option and the resulting histories to the players
                    SendActionToPlayers(gameData.PlayerId, gameData.GameDataType, gameData.GameDataObject);
                    SendHistoryToPlayers();

                    // 5. Send the current available options
                    if (_game.State == State.RUNNING)
                    {
                        SendOptionsOrChoicesToPlayers();
                    }
                    else
                    {
                        Stop();
                    }
                    break;

                case GameDataType.PowerChoice:
                    var powerChoices = JsonConvert.DeserializeObject<PowerChoices>(gameData.GameDataObject);
                    var choiceTask = SabberStoneConverter.CreatePlayerTaskChoice(_game, gameData.PlayerId, powerChoices.ChoiceType, powerChoices.Entities);

                    _game.Process(choiceTask);

                    // if mulligan has been finished!
                    if (_game.Step == Step.BEGIN_MULLIGAN
                     && _game.Player1.MulliganState == Mulligan.DONE
                     && _game.Player2.MulliganState == Mulligan.DONE)
                    {
                        _game.MainBegin();
                    }

                    SendActionToPlayers(gameData.PlayerId, gameData.GameDataType, gameData.GameDataObject);
                    SendHistoryToPlayers();

                    if (_game.State == State.RUNNING)
                    {
                        SendOptionsOrChoicesToPlayers();
                    }
                    else
                    {
                        Stop();
                    }
                    break;

                case GameDataType.Concede:
                    break;

                default:
                    break;
            }
        }

        private void SendActionToPlayers(int playerId, GameDataType gameDataType, string gameDataObject)
        {
            SendGameData(Player1, playerId, MsgType.InGame, true, gameDataType, gameDataObject);
            SendGameData(Player2, playerId, MsgType.InGame, true, gameDataType, gameDataObject);
        }

        public void SendHistoryToPlayers()
        {
            // send player history to both players
            string powerHistory = JsonConvert.SerializeObject(_game.PowerHistory.Last);
            //_game.PowerHistory.Last.ForEach(p => Log.Info(p.Print()));

            SendGameData(Player1, MsgType.InGame, true, GameDataType.PowerHistory, powerHistory);
            SendGameData(Player2, MsgType.InGame, true, GameDataType.PowerHistory, powerHistory);
            //Thread.Sleep(100);

        }

        public void SendOptionsOrChoicesToPlayers()
        {
            if (_game.Player1.Choice != null)
            {
                //Log.Debug($"sending Choices to player 1");
                var powerChoicesPlayer1 = PowerChoicesBuilder.EntityChoices(_game, _game.Player1.Choice);
                SendGameData(Player1, MsgType.InGame, true, GameDataType.PowerChoices, JsonConvert.SerializeObject(new PowerChoices() { Index = powerChoicesPlayer1.Index, ChoiceType = powerChoicesPlayer1.ChoiceType, Entities = powerChoicesPlayer1.Entities }));
            }
            else
            {
                var powerAllOptionsPlayer1 = PowerOptionsBuilder.AllOptions(_game, _game.Player1.Options());
                SendGameData(Player1, MsgType.InGame, true, GameDataType.PowerOptions, JsonConvert.SerializeObject(new PowerOptions() { Index = powerAllOptionsPlayer1.Index, PowerOptionList = powerAllOptionsPlayer1.PowerOptionList }));
            }

            if (_game.Player2.Choice != null)
            {
                //Log.Debug($"sending Choices to player 2");
                var powerChoicesPlayer2 = PowerChoicesBuilder.EntityChoices(_game, _game.Player2.Choice);
                SendGameData(Player2, MsgType.InGame, true, GameDataType.PowerChoices, JsonConvert.SerializeObject(new PowerChoices() { Index = powerChoicesPlayer2.Index, ChoiceType = powerChoicesPlayer2.ChoiceType, Entities = powerChoicesPlayer2.Entities }));
            }
            else
            {
                var powerAllOptionsPlayer2 = PowerOptionsBuilder.AllOptions(_game, _game.Player2.Options());
                SendGameData(Player2, MsgType.InGame, true, GameDataType.PowerOptions, JsonConvert.SerializeObject(new PowerOptions() { Index = powerAllOptionsPlayer2.Index, PowerOptionList = powerAllOptionsPlayer2.PowerOptionList }));
            }
        }

        public void SendGameData(UserClient player, MsgType messageType, bool messageState, GameDataType gameDataType, string gameDataObject = "")
        {
            SendGameData(player, player.PlayerId, messageType, messageState, gameDataType, gameDataObject);
        }

        public void SendGameData(UserClient player, int playerId, MsgType messageType, bool messageState, GameDataType gameDataType, string gameDataObject = "")
        {
            player.responseQueue.Enqueue(new GameServerStream()
            {
                MessageType = messageType,
                MessageState = messageState,
                Message = JsonConvert.SerializeObject(new GameData()
                {
                    GameId = GameId,
                    PlayerId = playerId,
                    GameDataType = gameDataType,
                    GameDataObject = gameDataObject
                })
            });
        }

        public void Stop()
        {
            // stop game for both players now!
            Log.Warn($"[_gameId:{GameId}] should be stopped here, isn't implemented!!!");

            //Log.Warn($"Server: {_game.Hash()}");

            SendGameData(Player1, MsgType.InGame, true, GameDataType.Result, "");
            SendGameData(Player2, MsgType.InGame, true, GameDataType.Result, "");

            Player1.PlayerState = PlayerState.Quit;
            Player2.PlayerState = PlayerState.Quit;

            //            Player1.Connection.Send(DataPacketBuilder.RequestServerGameStop(_id, _token, GameId, _game.Player1.PlayState, _game.Player2.PlayState));
            //            Player2.Connection.Send(DataPacketBuilder.RequestServerGameStop(_id, _token, GameId, _game.Player1.PlayState, _game.Player2.PlayState));
        }

        //public PlayerTask ProcessPowerOptionsData(int sendOptionId, int sendOptionMainOption, int sendOptionTarget, int sendOptionPosition, int sendOptionSubOption)

    }
}