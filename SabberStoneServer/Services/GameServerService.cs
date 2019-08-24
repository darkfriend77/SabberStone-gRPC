using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using log4net;
using Newtonsoft.Json;
using SabberStoneContract.Model;
using SabberStoneCore.Model;
using SabberStoneServer.Core;

namespace SabberStoneServer.Services
{
    public class GameServerServiceImpl : GameServerService.GameServerServiceBase
    {
        private static readonly ILog Log = Logger.Instance.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static ConcurrentDictionary<string, UserClient> _registredUsers;

        public ICollection<UserClient> RegistredUsers => _registredUsers.Values;

        public Action<MsgType, bool, GameData> ProcessGameData { get; internal set; }

        public Func<int, int, MatchGame> GetMatchGame { get; internal set; }

        private int _index = 10000;
        public int NextSessionIndex => _index++;

        public GameServerServiceImpl()
        {
            _registredUsers = new ConcurrentDictionary<string, UserClient>();
        }

        public override Task<ServerReply> Ping(ServerRequest request, ServerCallContext context)
        {
            Log.Info(context.Peer);

            var reply = new ServerReply
            {
                RequestState = true,
                RequestMessage = "Ping",
            };

            return Task.FromResult(reply);
        }

        public override Task<ServerReply> Disconnect(ServerRequest request, ServerCallContext context)
        {
            if (!TokenAuthentification(context.RequestHeaders, out string clientTokenValue))
            {
                return Task.FromResult(new ServerReply() { RequestState = false });
            }

            if (!_registredUsers.TryGetValue(clientTokenValue, out UserClient userDataInfo))
            {
                return Task.FromResult(new ServerReply() { RequestState = false });
            }

            // stop stream channel
            userDataInfo.CancellationTokenSource.Cancel();

            var reply = new ServerReply
            {
                RequestState = true
            };

            return Task.FromResult(reply);
        }

        public override Task<AuthReply> Authentication(AuthRequest request, ServerCallContext context)
        {
            // invalid accountname
            if (request.AccountName == null || request.AccountName.Length < 3)
            {
                Log.Warn($"{request.AccountName} is invalid!");
                return Task.FromResult(new AuthReply() { RequestState = false });
            }

            var user = _registredUsers.Values.ToList().Find(p => p.AccountName == request.AccountName);

            // already authentificated accounts
            if (user != null)
            {
                if (user.Peer.Equals(context.Peer))
                {
                    Log.Warn($"{request.AccountName} is already registred, with the same peer!");
                    return Task.FromResult(new AuthReply { RequestState = false });
                }

                // TODO same account with a new connection
                Log.Warn($"{request.AccountName} is already registred, with a different peer!");
                return Task.FromResult(new AuthReply { RequestState = false });
            }

            var sessionId = NextSessionIndex;
            var userInfo = new UserClient
            {
                Peer = context.Peer,
                Token = Helper.ComputeSha256Hash(sessionId + request.AccountName + context.Peer),
                SessionId = sessionId,
                AccountName = request.AccountName,
                UserState = UserState.Connected,
                GameId = -1,
                DeckType = DeckType.Random,
                DeckData = string.Empty,
                PlayerState = PlayerState.None,
                PlayerId = -1
            };


            // failed registration
            if (!_registredUsers.TryAdd(userInfo.Token, userInfo))
            {
                Log.Warn($"failed to register user with account {request.AccountName}!");
                return Task.FromResult(new AuthReply { RequestState = false });
            }

            Log.Info($"Successfully registred user with account {request.AccountName}!");

            var reply = new AuthReply
            {
                RequestState = true,
                RequestMessage = string.Empty,
                SessionId = userInfo.SessionId,
                SessionToken = userInfo.Token
            };

            return Task.FromResult(reply);
        }

        public override Task<MatchGameReply> MatchGame(MatchGameRequest request, ServerCallContext context)
        {
            if (!TokenAuthentification(context.RequestHeaders, out string clientTokenValue))
            {
                return Task.FromResult(new MatchGameReply() { RequestState = false });
            }

            if (!_registredUsers.TryGetValue(clientTokenValue, out UserClient userDataInfo))
            {
                Log.Info($"couldn't get user data info!");
                return Task.FromResult(new MatchGameReply() { RequestState = false });
            }

            return Task.FromResult(new MatchGameReply()
            {
                RequestState = true,
                MatchGame = GetMatchGame(userDataInfo.GameId, userDataInfo.PlayerId)
            });
        }

        public override async Task GameServerChannel(IAsyncStreamReader<GameServerStream> requestStream, IServerStreamWriter<GameServerStream> responseStream, ServerCallContext context)
        {
            if (!TokenAuthentification(context.RequestHeaders, out string clientTokenValue))
            {
                return;
            }

            if (!_registredUsers.TryGetValue(clientTokenValue, out UserClient userDataInfo))
            {
                Log.Info($"couldn't get user data info!");
                return;
            }

            if (userDataInfo.ResponseStreamWriterTask != null && userDataInfo.ResponseStreamWriterTask.Status == TaskStatus.Running)
            {
                Log.Info($"already running ResponseStreamWriterTask[{userDataInfo.ResponseStreamWriterTask.Status}]!");
                return;
            }

            userDataInfo.ResponseStreamWriterTask = new Task(async () =>
            {
                while (!userDataInfo.CancellationToken.IsCancellationRequested)
                {
                    if (userDataInfo.responseQueue.TryDequeue(out GameServerStream gameServerStream))
                    {
                        await responseStream.WriteAsync(gameServerStream);
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
            });
            userDataInfo.ResponseStreamWriterTask.Start();

            await Task.Run(async () =>
            {
                while (await requestStream.MoveNext(userDataInfo.CancellationToken))
                {
                    try
                    {
                        var response = ProcessRequest(requestStream.Current);
                        if (response != null)
                        {
                            userDataInfo.responseQueue.Enqueue(response);
                        }
                    }
                    catch (RpcException exception)
                    {
                        if (exception.StatusCode != StatusCode.Cancelled)
                        {
                            Log.Error(exception.ToString());
                        }
                    }
                }
            });

            Log.Info($"gameserver channel closed for user!");
        }

        private GameServerStream ProcessRequest(GameServerStream current)
        {
            switch (current.MessageType)
            {
                case MsgType.Initialisation:
                    return new GameServerStream() { MessageType = MsgType.Initialisation, MessageState = true, Message = string.Empty };
                case MsgType.Invitation:
                case MsgType.InGame:
                    ProcessGameData(current.MessageType, current.MessageState, JsonConvert.DeserializeObject<GameData>(current.Message));
                    return null;
                default:
                    return new GameServerStream() { MessageType = MsgType.Initialisation, MessageState = false, Message = string.Empty };
            }
        }

        public override Task<ServerReply> GameQueue(QueueRequest request, ServerCallContext context)
        {
            if (!TokenAuthentification(context.RequestHeaders, out string clientTokenValue))
            {
                return Task.FromResult(new ServerReply
                {
                    RequestState = false,
                    RequestMessage = string.Empty
                });
            }

            if (!_registredUsers.TryGetValue(clientTokenValue, out UserClient userDataInfo))
            {
                Log.Info($"couldn't get user data info!");
                return Task.FromResult(new ServerReply
                {
                    RequestState = false,
                    RequestMessage = string.Empty
                });
            }

            if (userDataInfo.ResponseStreamWriterTask.Status != TaskStatus.Running)
            {
                Log.Info($"User hasn't established a channel initialisation!");
                return Task.FromResult(new ServerReply
                {
                    RequestState = false,
                    RequestMessage = string.Empty
                });
            }

            // updated user informations
            userDataInfo.UserState = UserState.Queued;
            userDataInfo.DeckType = request.DeckType;
            userDataInfo.DeckData = request.DeckData;

            return Task.FromResult(new ServerReply
            {
                RequestState = true,
                RequestMessage = string.Empty
            });
        }

        private bool TokenAuthentification(Metadata metaData, out string clientTokenValue)
        {
            clientTokenValue = null;

            var clientToken = metaData.SingleOrDefault(e => e.Key == "token");
            if (clientToken == null || clientToken.Value.Length == 0 || !_registredUsers.ContainsKey(clientToken.Value))
            {
                Log.Info($"bad game server channel request, no valid token!");
                return false;
            }

            clientTokenValue = clientToken.Value;
            return true;

        }
    }

    public class UserClient : UserInfo
    {
        public string Token { get; set; }
        public string Peer { get; set; }

        public Task ResponseStreamWriterTask { get; set; }

        public ConcurrentQueue<GameServerStream> responseQueue;

        public CancellationToken CancellationToken => CancellationTokenSource.Token;

        public CancellationTokenSource CancellationTokenSource;

        public UserClient()
        {
            CancellationTokenSource = new CancellationTokenSource();
            responseQueue = new ConcurrentQueue<GameServerStream>();
        }

    }


}