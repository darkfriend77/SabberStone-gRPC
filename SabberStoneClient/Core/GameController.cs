using SabberStoneContract.Model;
using SabberStoneCore.Kettle;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SabberStoneClient.Core
{
    public class GameController
    {
        public ConcurrentQueue<IPowerHistoryEntry> HistoryEntries { get; }

        public PowerChoices PowerChoices { get; set; }

        public List<PowerOption> PowerOptionList { get; set; }

        public GameController()
        {
            HistoryEntries = new ConcurrentQueue<IPowerHistoryEntry>();
            PowerOptionList = new List<PowerOption>();
            PowerChoices = null;
        }

    }
}
