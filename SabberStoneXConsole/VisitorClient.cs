using System;
using System.Threading;
using SabberStoneContract.Client;
using SabberStoneContract.Core;

namespace SabberStoneXConsole
{
    public class VisitorClient : GameClient
    {
        private string _accountName;

        public VisitorClient(string accountName, string targetIp, int port, GameController gameController) : base(targetIp, port, gameController)
        {
            _accountName = accountName;
        }

        public override void CallGameClientState(GameClientState oldState, GameClientState newState)
        {
            switch (newState)
            {
                case GameClientState.None:
                    break;

                case GameClientState.Connected:
                    Register(_accountName, "");
                    break;

                case GameClientState.Registered:
                    break;

                case GameClientState.Queued:
                    break;

                case GameClientState.Placed:
                    break;

                case GameClientState.Invited:
                    break;

                case GameClientState.InGame:
                    break;
            }
        }

    }
}