using SabberStoneContract.Model;
using SabberStoneCore.Kettle;
using System.Collections.Generic;

namespace SabberStoneClient.AI
{
    public interface ISabberStoneAI
    {
        PowerOptionChoice PowerOptions(List<PowerOption> powerOptionList);
        PowerChoices PowerChoices(PowerChoices powerChoices);
    }
}