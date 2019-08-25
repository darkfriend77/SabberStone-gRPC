using Newtonsoft.Json;
using SabberStoneContract.Interface;
using SabberStoneContract.Model;
using SabberStoneCore.Kettle;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace SabberStoneContract.Client
{
    public class GameController
    {
        private Action<MsgType, bool, string> _sendGameMessage { get; set; }

        private List<UserInfo> _userInfos;

        public int GameId { get; set; }

        public int PlayerId { get; set; }

        public UserInfo MyUserInfo => _userInfos.FirstOrDefault(p => p.PlayerId == PlayerId);

        public UserInfo OpUserInfo => _userInfos.FirstOrDefault(p => p.PlayerId != PlayerId);

        public ConcurrentQueue<IPowerHistoryEntry> HistoryEntries { get; }

        public PowerChoices PowerChoices { get; set; }

        public PowerOptions PowerOptions { get; set; }

        //public List<PowerOption> PowerOptionList { get; set; }

        public IGameAI GameAI { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameAI"></param>
        /// <param name="sendGameMessage"></param>
        public GameController(IGameAI gameAI)
        {
            _userInfos = new List<UserInfo>();

            GameAI = gameAI ?? new RandomAI();
            HistoryEntries = new ConcurrentQueue<IPowerHistoryEntry>();
            PowerOptions = null;
            PowerChoices = null;
        }

        public void Reset()
        {
            GameId = 0;
            PlayerId = 0;

            _userInfos.Clear();
            while (!HistoryEntries.IsEmpty)
            {
                HistoryEntries.TryDequeue(out _);
            }
            PowerOptions = null;
            PowerChoices = null;
        }

        public void SetSendGameMessage(Action<MsgType, bool, string> sendGameMessage)
        {
            _sendGameMessage = sendGameMessage;
        }

        public void SetUserInfos(List<UserInfo> userInfos)
        {
            _userInfos = userInfos;
            CallInitialisation();
        }

        public virtual async void CallInitialisation()
        {
            await Task.Run(() =>
            {
            });
        }

        public void SetPowerHistory(List<IPowerHistoryEntry> powerHistoryEntries)
        {
            powerHistoryEntries.ForEach(p => HistoryEntries.Enqueue(p));
            CallPowerHistory();
        }

        public virtual async void CallPowerHistory()
        {
            await Task.Run(() =>
            {
            });
        }

        public void SetPowerChoices(PowerChoices powerChoices)
        {
            PowerChoices = powerChoices;
            CallPowerChoices();
        }

        public virtual async void CallPowerChoices()
        {
            await Task.Run(() =>
            {
                SendPowerChoicesChoice(GameAI.PowerChoices(PowerChoices));
            });
        }

        public void SetPowerOptions(PowerOptions powerOptions)
        {
            PowerOptions = powerOptions;
            if (PowerOptions.PowerOptionList != null &&
                PowerOptions.PowerOptionList.Count > 0)
            {
                CallPowerOptions();
            }
        }

        public virtual async void CallPowerOptions()
        {
            await Task.Run(() =>
            {
                SendPowerOptionChoice(GameAI.PowerOptions(PowerOptions.PowerOptionList));
            });
        }

        public void SendInvitationReply(bool accept)
        {
            _sendGameMessage(MsgType.Invitation, accept,
                JsonConvert.SerializeObject(
                    new GameData
                    {
                        GameId = GameId,
                        PlayerId = PlayerId,
                        GameDataType = GameDataType.None
                    }));
        }

        public void SendPowerChoicesChoice(PowerChoices powerChoices)
        {
            PowerChoices = null;
            _sendGameMessage(MsgType.InGame, true,
                JsonConvert.SerializeObject(
                    new GameData()
                    {
                        GameId = GameId,
                        PlayerId = PlayerId,
                        GameDataType = GameDataType.PowerChoices,
                        GameDataObject = JsonConvert.SerializeObject(powerChoices)
                    }));
        }

        public void SendPowerOptionChoice(PowerOptionChoice powerOptionChoice)
        {
            PowerOptions = null;
            _sendGameMessage(MsgType.InGame, true,
                JsonConvert.SerializeObject(
                    new GameData()
                    {
                        GameId = GameId,
                        PlayerId = PlayerId,
                        GameDataType = GameDataType.PowerOptions,
                        GameDataObject = JsonConvert.SerializeObject(powerOptionChoice)
                    }));
        }
    }
}
