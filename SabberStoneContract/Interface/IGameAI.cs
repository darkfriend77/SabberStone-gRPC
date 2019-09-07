using SabberStoneContract.Model;
using SabberStoneCore.Kettle;
using System.Collections.Generic;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

namespace SabberStoneContract.Interface
{
    public interface IGameAI
    {
        PowerOptionChoice PowerOptions(Game game, List<PowerOption> powerOptionList);
        PowerChoices PowerChoices(Game game, PowerChoices powerChoices);
        PlayerTask DetermineAction(Game game);
    }
}