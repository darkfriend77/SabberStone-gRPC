using SabberStoneContract.Model;
using SabberStoneCore.Kettle;
using System.Collections.Generic;

namespace SabberStoneClient.Interface
{
    public interface IGameAI
    {
        PowerOptionChoice PowerOptions(List<PowerOption> powerOptionList);
        PowerChoices PowerChoices(PowerChoices powerChoices);
    }
}