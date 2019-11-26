using SabberStoneContract.Client;
using SabberStoneContract.Core;

namespace SabberStoneClient
{
    public class TestGameClient : GameClient
    {
        public TestGameClient(string ip, int port, GameController gameController) : base(ip, port, gameController)
        {
        }
    }
}
