using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SabberStoneContract.Client;
using SabberStoneContract.Core;
using SabberStoneContract.Interface;

namespace SabberStoneXConsole
{
    public class AIClient : GameClient
    {
        private int _remainingGame;

        public string CurrentDeckString { get; }

        //public event Action<AIClient> OnMatchFinished;

        public AIClient(string ip, int port, Credential credential, GameController gameController, string deckString) : base(ip, port, credential, gameController)
        {
            CurrentDeckString = deckString;
        }

        public static async Task<AIClient> Initialise(string ip, int port, Credential credential, IGameAI ai, string deckString)
        {
            var client = new AIClient(ip, port, credential,
                new GameController(ai), deckString);

            client.Connect();
            await client.QueueAsync(GameType.Normal, deckString);

            return client;
        }

        public override void OnMatchFinished()
        {
            if (_remainingGame == 0)
                Disconnect();
            else
                Queue(GameType.Normal, CurrentDeckString);
        }
    }
}
