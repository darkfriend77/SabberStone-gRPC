using SabberStoneContract.Client;
using SabberStoneContract.Interface;

namespace SabberStoneContract
{
    public class TestGameController : GameController
    {
        public TestGameController(IGameAI gameAI) : base(gameAI)
        {

        }

    }
}
