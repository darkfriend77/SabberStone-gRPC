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
        public int GameId { get; set; }

        public int PlayerId { get; set; }

        private List<UserInfo> _userInfos;

        public UserInfo MyUserInfo => _userInfos.FirstOrDefault(p => p.PlayerId == PlayerId);

        public UserInfo OpUserInfo => _userInfos.FirstOrDefault(p => p.PlayerId != PlayerId);

        private ConcurrentQueue<IPowerHistoryEntry> _historyEntries;

        private PowerChoices _powerChoices;

        private List<PowerOption> _powerOptionList;

        public IGameAI SabberStoneAI { get; set; }

        private Action<MsgType, bool, string> _sendGameMessage { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sabberStoneAI"></param>
        /// <param name="sendGameMessage"></param>
        public GameController(IGameAI sabberStoneAI)
        {
            SabberStoneAI = sabberStoneAI ?? new RandomAI();

            _userInfos = new List<UserInfo>();
            _historyEntries = new ConcurrentQueue<IPowerHistoryEntry>();
            _powerOptionList = new List<PowerOption>();
            _powerChoices = null;
        }

        public void Reset()
        {
            GameId = 0;
            PlayerId = 0;

            _userInfos.Clear();
            while (!_historyEntries.IsEmpty)
            {
                _historyEntries.TryDequeue(out _);
            }
            _powerOptionList.Clear();
            _powerChoices = null;
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
            powerHistoryEntries.ForEach(p => _historyEntries.Enqueue(p));
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
            _powerChoices = powerChoices;
            CallPowerChoices();
        }

        public virtual async void CallPowerChoices()
        {
            await Task.Run(() =>
            {
                SendPowerChoicesChoice(SabberStoneAI.PowerChoices(_powerChoices));
            });
        }

        public void SetPowerOptions(PowerOptions powerOptions)
        {
            _powerOptionList = powerOptions.PowerOptionList;
            if (_powerOptionList != null &&
                _powerOptionList.Count > 0)
            {
                CallPowerOptions();
            }
        }

        public virtual async void CallPowerOptions()
        {
            await Task.Run(() =>
            {
                SendPowerOptionChoice(SabberStoneAI.PowerOptions(_powerOptionList));
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
            _powerChoices = null;
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
            _powerOptionList.Clear();
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
