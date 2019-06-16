using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace SabberStoneContract.Model
{
    public enum UserState
    {
        None,
        Queued,
        Invited,
        Prepared,
        InGame
    }

    public enum PlayerState
    {
        None,
        Invitation,
        Game,
        Quit
    }

    public class UserDataInfo : UserInfo
    {
        public virtual string Token { get; set; }
        public virtual string Peer { get; set; }
        public virtual IServerStreamWriter<GameServerStream> ResponseStream { get; set; }
    }

    public class UserInfo
    {
        public virtual int SessionId { get; set; }
        public virtual string AccountName { get; set; }
        public virtual UserState UserState { get; set; }
        public virtual int GameId { get; set; }
        public virtual DeckType DeckType { get; set; }
        public virtual string DeckData { get; set; }
        public virtual PlayerState PlayerState { get; set; }
    }

    public enum GameDataType
    {
        None
    }

    public class GameData
    {
        public virtual int GameId { get; set; }
        public virtual int PlayerId { get; set; }
        public virtual GameDataType GameDataType { get; set; }
    }
}
