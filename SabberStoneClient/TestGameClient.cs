using SabberStoneContract.Client;
using SabberStoneContract.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace SabberStoneClient
{
    public class TestGameClient : GameClient
    {
        public TestGameClient(string targetIp, int port, GameController gameController) : base(targetIp, port, gameController)
        {
        }
    }
}
