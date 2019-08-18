using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using log4net;
using SabberStoneClient.Core;
using SabberStoneContract.Model;
using SabberStoneCore.Kettle;

namespace SabberStoneClient.Interface
{
    public class RandomAI : IGameAI
    {
        private static readonly ILog Log = Logger.Instance.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Random _random;

        public RandomAI()
        {
            _random = new Random();
        }

        public PowerChoices PowerChoices(PowerChoices powerChoices)
        {
            var powerChoicesId = _random.Next(powerChoices.Entities.Count);
            Log.Info($"SendPowerChoicesChoice[RandomAI] -> choices:{powerChoicesId} {powerChoices.ChoiceType}");

            return new PowerChoices() { ChoiceType = powerChoices.ChoiceType, Entities = new List<int>() { powerChoices.Entities[powerChoicesId] } };
        }

        public PowerOptionChoice PowerOptions(List<PowerOption> powerOptionList)
        {
            var powerOptionId = _random.Next(powerOptionList.Count);
            var powerOption = powerOptionList.ElementAt(powerOptionId);
            var target = powerOption.MainOption?.Targets != null && powerOption.MainOption.Targets.Count > 0
                ? powerOption.MainOption.Targets.ElementAt(_random.Next(powerOption.MainOption.Targets.Count))
                : 0;
            var subOption = powerOption.SubOptions != null && powerOption.SubOptions.Count > 0
                ? _random.Next(powerOption.SubOptions.Count)
                : 0;
            Log.Info($"SendPowerOptionChoice[RandomAI] -> target:{target}, position:0, suboption: {subOption} {powerOption.OptionType}");
            return new PowerOptionChoice() { PowerOption = powerOption, Target = target, Position = 0, SubOption = subOption };
        }
    }
}