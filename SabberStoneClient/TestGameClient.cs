using SabberStoneContract.Client;
using SabberStoneContract.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace SabberStoneClient
{
    public class TestGameClient : GameClient
    {
        public TestGameClient(string ip, int port, GameController gameController) : base(ip, port, gameController)
        {
        }
    }
}
