using log4net;
using Newtonsoft.Json;
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

namespace SabberStoneServer.Services
{
    public partial class MatchGameService
    {
        private static readonly ILog Log = Logger.Instance.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public int GameId { get; }

        private readonly GameServerServiceImpl _gameServerService;

        private readonly Random _random;

        public UserDataInfo Player1 { get; }

        public PlayState Play1State => _game.Player1.PlayState;

        private PowerAllOptions _powerAllOptionsPlayer1;

        public UserDataInfo Player2 { get; }

        public PlayState Play2State => _game.Player2.PlayState;

        private PowerAllOptions _powerAllOptionsPlayer2;

        private Game _game;

        public MatchGame MatchGame(int playerId) => CreateMatchGameReply();

        private readonly int _id;

        private readonly string _token;

        private UserDataInfo UserById(int id) => Player1.PlayerId == id ? Player1 : Player2.PlayerId == id ? Player2 : null;

        public bool IsFinished => Player1.PlayerState == PlayerState.Quit && Player2.PlayerState == PlayerState.Quit;

        public MatchGameService(GameServerServiceImpl gameServerService, int index, UserDataInfo player1, UserDataInfo player2)
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

        public void Start()
        {
            Log.Info($"[_gameId:{GameId}] Game creation is happening in a few seconds!!!");
            var newGame = new Game(new GameConfig
            {
                //StartPlayer = 1,
                FormatType = FormatType.FT_STANDARD,
                Player1HeroClass = Cards.HeroClasses[_random.Next(9)],
                Player1Deck = new List<Card>(),
                Player2HeroClass = Cards.HeroClasses[_random.Next(9)],
                Player2Deck = new List<Card>(),
                SkipMulligan = true,
                Shuffle = true,
                FillDecks = true,
                Logging = true,
                History = true
            });

            // don't start when game is null
            if (_game != null)
            {
                return;
            }

            _game = newGame;

            Log.Info($"[_gameId:{GameId}] Game creation done!");
            _game.StartGame();

            SendPowerHistoryToPlayers();

            SendPowerOptionsToPlayers();
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
                Start();
                Thread.Sleep(500);

                // we send over opponend user info which is reduced to open available informations
                SendGameData(Player1, MsgType.InGame, true, GameDataType.Initialisation, JsonConvert.SerializeObject(new List<UserInfo> { Player1, Player2.OpenUserInfo() }));
                SendGameData(Player2, MsgType.InGame, true, GameDataType.Initialisation, JsonConvert.SerializeObject(new List<UserInfo> { Player1.OpenUserInfo(), Player2 }));
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
                case GameDataType.PowerOptions:
                    var powerOptionChoice = JsonConvert.DeserializeObject<PowerOptionChoice>(gameData.GameDataObject);
                    var optionTask = ProcessPowerOptionsData(powerOptionChoice.PowerOption, powerOptionChoice.Target, powerOptionChoice.Position, powerOptionChoice.SubOption);
                    _game.Process(optionTask);

                    if (powerOptionChoice.PowerOption.OptionType == OptionType.END_TURN)
                    {
                        Log.Info($"State[{_game.State}]-T[{_game.Turn}] Hero1: {_game.Player1.Hero.Health} HP // Hero2: {_game.Player2.Hero.Health} HP");
                    }

                    SendPowerHistoryToPlayers();

                    if (_game.State == State.RUNNING)
                    {
                        SendPowerChoicesToPlayers();
                        SendPowerOptionsToPlayers();
                    }
                    else
                    {
                        Stop();
                    }
                    break;

                case GameDataType.PowerChoices:
                    var powerChoices = JsonConvert.DeserializeObject<PowerChoices>(gameData.GameDataObject);
                    var choiceTask = ProcessPowerChoiceData(powerChoices.ChoiceType, powerChoices.Entities);
                    _game.Process(choiceTask);

                    SendPowerHistoryToPlayers();

                    if (_game.State == State.RUNNING)
                    {
                        SendPowerChoicesToPlayers();
                        SendPowerOptionsToPlayers();
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

        public void SendPowerHistoryToPlayers()
        {
            // send player history to both players
            string powerHistory = JsonConvert.SerializeObject(_game.PowerHistory.Last);
            SendGameData(Player1, MsgType.InGame, true, GameDataType.PowerHistory, powerHistory);
            SendGameData(Player2, MsgType.InGame, true, GameDataType.PowerHistory, powerHistory);
            Thread.Sleep(100);
        }

        public void SendPowerOptionsToPlayers()
        {
            _powerAllOptionsPlayer1 = PowerOptionsBuilder.AllOptions(_game, _game.Player1.Options());
            SendGameData(Player1, MsgType.InGame, true, GameDataType.PowerOptions, JsonConvert.SerializeObject(new PowerOptions() { Index = _powerAllOptionsPlayer1.Index, PowerOptionList = _powerAllOptionsPlayer1.PowerOptionList }));
            _powerAllOptionsPlayer2 = PowerOptionsBuilder.AllOptions(_game, _game.Player2.Options());
            SendGameData(Player2, MsgType.InGame, true, GameDataType.PowerOptions, JsonConvert.SerializeObject(new PowerOptions() { Index = _powerAllOptionsPlayer2.Index, PowerOptionList = _powerAllOptionsPlayer2.PowerOptionList }));
            Thread.Sleep(100);
        }

        private void SendPowerChoicesToPlayers()
        {
            if (_game.Player1.Choice != null)
            {
                SendGameData(Player1, MsgType.InGame, true, GameDataType.PowerChoices, JsonConvert.SerializeObject(new PowerChoices() { ChoiceType = _game.Player1.Choice.ChoiceType, Entities = _game.Player1.Choice.Choices }));
            }

            if (_game.Player2.Choice != null)
            {
                SendGameData(Player2, MsgType.InGame, true, GameDataType.PowerChoices, JsonConvert.SerializeObject(new PowerChoices() { ChoiceType = _game.Player2.Choice.ChoiceType, Entities = _game.Player2.Choice.Choices }));
            }
            Thread.Sleep(100);
        }

        public void SendGameData(UserDataInfo player, MsgType messageType, bool messageState, GameDataType gameDataType, string gameDataObject = "")
        {
            player.ResponseStream.WriteAsync(new GameServerStream()
            {
                MessageType = messageType,
                MessageState = messageState,
                Message = JsonConvert.SerializeObject(new GameData() { GameId = GameId, PlayerId = player.PlayerId, GameDataType = gameDataType, GameDataObject = gameDataObject })
            });
        }

        public void Stop()
        {
            // stop game for both players now!
            Log.Warn($"[_gameId:{GameId}] should be stopped here, isn't implemented!!!");

            SendGameData(Player1, MsgType.InGame, true, GameDataType.Result, "");
            SendGameData(Player2, MsgType.InGame, true, GameDataType.Result, "");

            Player1.PlayerState = PlayerState.Quit;
            Player2.PlayerState = PlayerState.Quit;

            //            Player1.Connection.Send(DataPacketBuilder.RequestServerGameStop(_id, _token, GameId, _game.Player1.PlayState, _game.Player2.PlayState));
            //            Player2.Connection.Send(DataPacketBuilder.RequestServerGameStop(_id, _token, GameId, _game.Player1.PlayState, _game.Player2.PlayState));
        }

        //public PlayerTask ProcessPowerOptionsData(int sendOptionId, int sendOptionMainOption, int sendOptionTarget, int sendOptionPosition, int sendOptionSubOption)
        public PlayerTask ProcessPowerOptionsData(PowerOption powerOption, int sendOptionTarget, int sendOptionPosition, int sendOptionSubOption)
        {

            //var allOptions = _game.AllOptionsMap[sendOptionId];
            //var tasks = allOptions.PlayerTaskList;
            //var powerOption = allOptions.PowerOptionList[sendOptionMainOption];
            var optionType = powerOption.OptionType;

            PlayerTask task = null;
            switch (optionType)
            {
                case OptionType.END_TURN:
                    task = EndTurnTask.Any(_game.CurrentPlayer);
                    break;

                case OptionType.POWER:
                    var mainOption = powerOption.MainOption;
                    var source = _game.IdEntityDic[mainOption.EntityId];
                    var target = sendOptionTarget > 0 ? (ICharacter)_game.IdEntityDic[sendOptionTarget] : null;
                    var subObtions = powerOption.SubOptions;

                    if (source.Zone?.Type == Zone.PLAY)
                    {
                        task = MinionAttackTask.Any(_game.CurrentPlayer, source, target);
                    }
                    else
                    {
                        switch (source.Card.Type)
                        {
                            case CardType.HERO:
                                task = target != null
                                    ? (PlayerTask)HeroAttackTask.Any(_game.CurrentPlayer, target)
                                    : PlayCardTask.Any(_game.CurrentPlayer, source);
                                break;

                            case CardType.HERO_POWER:
                                task = HeroPowerTask.Any(_game.CurrentPlayer, target);
                                break;

                            default:
                                task = PlayCardTask.Any(_game.CurrentPlayer, source, target, sendOptionPosition, sendOptionSubOption);
                                break;
                        }
                    }
                    break;

                case OptionType.PASS:
                    break;

                default:
                    throw new NotImplementedException();
            }

            //Log.Info($"{task?.FullPrint()}");

            return task;
        }

        private PlayerTask ProcessPowerChoiceData(ChoiceType choiceType, List<int> entities)
        {
            switch (choiceType)
            {
                case ChoiceType.MULLIGAN:
                    return ChooseTask.Mulligan(_game.CurrentPlayer, entities);

                case ChoiceType.GENERAL:
                    return ChooseTask.Pick(_game.CurrentPlayer, entities[0]);
                default:
                    return null;
            }
        }
    }
}