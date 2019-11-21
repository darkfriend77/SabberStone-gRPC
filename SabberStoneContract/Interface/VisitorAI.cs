using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneContract.Model;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneContract.Interface
{
    public class VisitorAI : IGameAI
    {
        public PowerChoices PowerChoices(Game game, PowerChoices powerChoices)
        {
            return null;
        }

        public void InitialiseAgent()
        {

        }

        public void InitialiseGame()
        {

        }

        public PlayerTask GetMove(Game game)
        {
            return null;
        }

        public PowerOptionChoice PowerOptions(Game game, List<PowerOption> powerOptionList)
        {
            return null;
        }
    }
}