using log4net;
using SabberStoneContract.Model;
using SabberStoneServer.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace SabberStoneServer.Services
{
    public class MatchMakerService
    {
        private static readonly ILog Log = Logger.Instance.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly GameServerServiceImpl _gameServerService;

        private readonly Timer _timer;

        private int _maxGamesPerCall = 5;

        private int _index = 10000;
        public int Index => _index++;

        private readonly ConcurrentDictionary<int, MatchGameService> _matchGames;

        public ICollection<MatchGameService> MatchGames => _matchGames.Values;

        public MatchMakerService(GameServerServiceImpl gameServerService)
        {
            _gameServerService = gameServerService;
            _gameServerService.ProcessGameData = ProcessGameData;
            _timer = new Timer((e) => { MatchMakerEvent(); }, null, Timeout.Infinite, Timeout.Infinite);
            _matchGames = new ConcurrentDictionary<int, MatchGameService>();
        }

        private void MatchMakerEvent()
        {
            var finishedMatches = _matchGames.Values.Where(matchGamesValue => matchGamesValue.IsFinished).ToList();
            finishedMatches.ForEach(p =>
            {
                if (_matchGames.TryRemove(p.GameId, out var finishedMatch))
                {
                    Log.Info($"[GameId:{finishedMatch.GameId}] finished, [{finishedMatch.Player1.AccountName}:{finishedMatch.Play1State}] vs [{finishedMatch.Player2.AccountName}:{finishedMatch.Play2State}].");
                }
            });

            var queuedUsers = _gameServerService.RegistredUsers.ToList().Where(user => user.UserState == UserState.Queued).ToList();
            if (queuedUsers.Count > 0)
            {
                Log.Info($"{queuedUsers.Count} users queued for matchmaking.");
            }

            for (int i = 0; i < _maxGamesPerCall && queuedUsers.Count > 1; i++)
            {
                var player1 = queuedUsers.ElementAt(0);
                var player2 = queuedUsers.ElementAt(1);
                queuedUsers.RemoveRange(0, 2);

                player1.UserState = UserState.Invited;
                player2.UserState = UserState.Invited;

                var gameId = Index;
                var matchgame = new MatchGameService(_gameServerService, gameId, player1, player2);
                if (_matchGames.TryAdd(gameId, matchgame))
                {
                    matchgame.Initialize();
                }
                else
                {
                    Log.Error($"Couldn't add [GameId:{gameId}] match game with {player1.AccountName} and {player2.AccountName}.");
                }
            }
        }

        public void Start(int callFrequenceSeconds = 7)
        {
            var startTimeSpan = TimeSpan.Zero;
            var periodTimeSpan = TimeSpan.FromSeconds(callFrequenceSeconds);

            _timer.Change(startTimeSpan, periodTimeSpan);
        }

        public void Stop()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void ProcessGameData(MessageType messageType, bool messageState, GameData gameData)
        {
            if (!_matchGames.TryGetValue(gameData.GameId, out var matchGame))
            {
                Log.Warn($"Couldn't find match game with [GameId:{gameData.GameId}]. Not processing game data.");
                return;
            }

            Log.Info($"Processing game data '{messageType}:{messageState}' for [GameId:{gameData.GameId}].");

            switch (messageType)
            {
                case MessageType.Invitation:
                    matchGame.InvitationReply(messageState, gameData);
                    break;

                case MessageType.InGame:
                    matchGame.ProcessGameData(gameData);
                    break;
            }


        }

    }
}
