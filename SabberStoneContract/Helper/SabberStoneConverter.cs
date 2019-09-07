using SabberStoneContract.Model;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using System;
using System.Collections.Generic;
using System.Text;

namespace SabberStoneContract.Helper
{
    public class SabberStoneConverter
    {
        public static Game CreateGame(UserInfo player1, UserInfo player2, GameConfigInfo gameConfigInfo)
        {
            var gameConfig = GameConfigBuilder.Create()
                .SetPlayer1(player1.AccountName, player1.DeckData)
                .SetPlayer2(player2.AccountName, player2.DeckData)
                .SkipMulligan(gameConfigInfo.SkipMulligan)
                .Shuffle(gameConfigInfo.Shuffle)
                .FillDecks(gameConfigInfo.FillDecks)
                .Logging(gameConfigInfo.Logging)
                .History(gameConfigInfo.History)
                .RandomSeed(gameConfigInfo.RandomSeed)
                .Build();

            return new Game(gameConfig);
        }

        public static PlayerTask CreatePlayerTaskOption(Game game, PowerOption powerOption, int sendOptionTarget, int sendOptionPosition, int sendOptionSubOption)
        {

            //var allOptions = _game.AllOptionsMap[sendOptionId];
            //var tasks = allOptions.PlayerTaskList;
            //var powerOption = allOptions.PowerOptionList[sendOptionMainOption];
            var optionType = powerOption.OptionType;

            PlayerTask task = null;
            switch (optionType)
            {
                case OptionType.END_TURN:
                    task = EndTurnTask.Any(game.CurrentPlayer);
                    break;

                case OptionType.POWER:
                    var mainOption = powerOption.MainOption;
                    var source = game.IdEntityDic[mainOption.EntityId];
                    var target = sendOptionTarget > 0 ? (ICharacter)game.IdEntityDic[sendOptionTarget] : null;
                    var subObtions = powerOption.SubOptions;

                    if (source.Zone?.Type == Zone.PLAY)
                    {
                        task = MinionAttackTask.Any(game.CurrentPlayer, source, target);
                    }
                    else
                    {
                        switch (source.Card.Type)
                        {
                            case CardType.HERO:
                                task = target != null
                                    ? (PlayerTask)HeroAttackTask.Any(game.CurrentPlayer, target)
                                    : PlayCardTask.Any(game.CurrentPlayer, source);
                                break;

                            case CardType.HERO_POWER:
                                task = HeroPowerTask.Any(game.CurrentPlayer, target);
                                break;

                            default:
                                task = PlayCardTask.Any(game.CurrentPlayer, source, target, sendOptionPosition, sendOptionSubOption);
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

        public static PlayerTask CreatePlayerTaskChoice(Game game, int PlayerId, ChoiceType choiceType, List<int> entities)
        {
            switch (choiceType)
            {
                case ChoiceType.MULLIGAN:
                    return ChooseTask.Mulligan(game.Player1.PlayerId == PlayerId ? game.Player1 : game.Player2.PlayerId == PlayerId ? game.Player2 : null, entities);

                case ChoiceType.GENERAL:
                    return ChooseTask.Pick(game.CurrentPlayer, entities[0]);
                default:
                    return null;
            }
        }
    }
}
