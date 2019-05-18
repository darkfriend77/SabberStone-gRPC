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

    public enum DeckType
    {
        None,
        Random,
        DeckString,
        CardIds
    }

    public enum PlayerState
    {
        None,
        Invitation,
        Config,
        Game,
        Quit
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
}
