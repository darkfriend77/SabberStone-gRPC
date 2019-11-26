using System;
using System.Threading;
using SabberStoneContract.Client;
using SabberStoneContract.Core;

namespace SabberStoneXConsole
{
    public class TestClient : GameClient
    {
        private string _accountName;
        public TestClient(string accountName, string targetIp, int port, GameController gameController) : base(targetIp, port, gameController)
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
                    if (oldState != GameClientState.InGame)
                    {
                        Thread.Sleep(200);
                        Queue(GameType.Normal, "AAEBAQcCrwSRvAIOHLACkQP/A44FqAXUBaQG7gbnB+8HgrACiLACub8CAA==");
                        //Queue();
                        // [EU Legend #1 Tempo Mage] AAEBAf0ECnH2Ar8D7AW5BuwHuQ36Dp4QixQKwwG7ApUD5gSWBfcNgQ6HD4kPkBAA
                    }
                    else
                    {
                        Disconnect();
                    }
                    break;

                case GameClientState.Queued:
                    break;
                case GameClientState.Invited:
                    break;
                case GameClientState.InGame:
                    break;
            }
        }

        private void Register()
        {
            throw new NotImplementedException();
        }
    }
}