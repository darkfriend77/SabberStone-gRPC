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

        public Action<MsgType, bool, GameData> ProcessGameData { get; internal set; }

        public Func<int, int, MatchGame> GetMatchGame { get; internal set; }

        private int _index = 10000;
        public int NextSessionIndex => _index++;

        private readonly ConcurrentDictionary<string, UserDataInfo> _registredUsers;

        public ICollection<UserDataInfo> RegistredUsers => _registredUsers.Values;

        public GameServerServiceImpl()
        {
            _registredUsers = new ConcurrentDictionary<string, UserDataInfo>();
        }

        public override Task<ServerReply> Ping(PingRequest request, ServerCallContext context)
        {
            Log.Info(context.Peer);

            var reply = new ServerReply
            {
                RequestState = true,
                RequestMessage = "Ping",
            };

            return Task.FromResult(reply);
        }

        enum BadRequestType
        {
            INVALID_ACCOUNT,
            SAME_PEER_REGISTRED,
            DIFF_PEER_REGISTRED
        }

        public override Task<AuthReply> Authentication(AuthRequest request, ServerCallContext context)
        {
            // invalid accountname
            if (request.AccountName == null || request.AccountName.Length < 3)
            {
                return Task.FromResult(new AuthReply()
                {
                    ServerReply = GetServerReply(context.Peer, BadRequestType.INVALID_ACCOUNT)
                });
            }

            var user = _registredUsers.Values.ToList().Find(p => p.AccountName == request.AccountName);

            // already authentificated accounts
            if (user != null)
            {
                if (user.Peer.Equals(context.Peer))
                {
                    return Task.FromResult(new AuthReply() { ServerReply = GetServerReply(context.Peer, BadRequestType.SAME_PEER_REGISTRED) });
                }

                // TODO same account with a new connection
                return Task.FromResult(new AuthReply() { ServerReply = GetServerReply(context.Peer, BadRequestType.DIFF_PEER_REGISTRED) });
            }

            var sessionId = NextSessionIndex;
            var userInfo = new UserDataInfo
            {
                Peer = context.Peer,
                Token = Helper.ComputeSha256Hash(sessionId + request.AccountName + context.Peer),
                SessionId = sessionId,
                AccountName = request.AccountName,
                UserState = UserState.None,
                GameId = -1,
                DeckType = DeckType.None,
                DeckData = string.Empty,
                PlayerState = PlayerState.None,
                PlayerId = -1
            };


            // failed registration
            if (!_registredUsers.TryAdd(userInfo.Token, userInfo))
            {
                Log.Warn($"failed to register user with account {request.AccountName}!");
                return Task.FromResult(new AuthReply() { ServerReply = new ServerReply() { RequestState = false } });
            }

            Log.Info($"Successfully registred user with account {request.AccountName}!");

            var reply = new AuthReply
            {
                ServerReply = new ServerReply() { RequestState = true },
                SessionId = userInfo.SessionId,
                SessionToken = userInfo.Token
            };

            return Task.FromResult(reply);
        }

        private ServerReply GetServerReply(string peer, BadRequestType badRequestType)
        {
            var serverReply = new ServerReply()
            {
                RequestState = false,
                RequestMessage = "unknown bad request"
            };
            switch (badRequestType)
            {
                case BadRequestType.INVALID_ACCOUNT:
                    serverReply.RequestMessage = "invalid authentification";
                    break;
                case BadRequestType.SAME_PEER_REGISTRED:
                    serverReply.RequestMessage = "already registred same peer";
                    break;
                case BadRequestType.DIFF_PEER_REGISTRED:
                    serverReply.RequestMessage = "already registred different peer";
                    break;
            }
            Log.Warn($"{peer}:[{badRequestType}] {serverReply.RequestMessage}");
            return serverReply;
        }

        public override Task<MatchGameReply> MatchGame(MatchGameRequest request, ServerCallContext context)
        {
            if (!TokenAuthentification(context.RequestHeaders, out string clientTokenValue))
            {
                return Task.FromResult(new MatchGameReply() { ServerReply = new ServerReply() { RequestState = false } });
            }

            if (!_registredUsers.TryGetValue(clientTokenValue, out UserDataInfo userDataInfo))
            {
                Log.Info($"couldn't get user data info!");
                return Task.FromResult(new MatchGameReply() { ServerReply = new ServerReply() { RequestState = false } });
            }

            return Task.FromResult(new MatchGameReply()
            {
                ServerReply = new ServerReply() { RequestState = true },
                MatchGame = GetMatchGame(userDataInfo.GameId, userDataInfo.PlayerId)
            });
        }

        public override async Task GameServerChannel(IAsyncStreamReader<GameServerStream> requestStream, IServerStreamWriter<GameServerStream> responseStream, ServerCallContext context)
        {
            if (!TokenAuthentification(context.RequestHeaders, out string clientTokenValue))
            {
                return;
            }

            if (!_registredUsers.TryGetValue(clientTokenValue, out UserDataInfo userDataInfo))
            {
                Log.Info($"couldn't get user data info!");
                return;
            }

            if (ClientManager.ClientDictionary.ContainsKey(clientTokenValue))
            {
                Log.Info($"bad game server channel request, token already registred!");
                return;
            }

            if (!ClientManager.ClientDictionary.TryAdd(clientTokenValue,
                new Client(requestStream, responseStream, context)))
            {
                Log.Info($"bad game server channel request, couldn't add to client manager!");
                return;
            };

            // adding the response stream to user data
            userDataInfo.ResponseStream = responseStream;

            var requestStreamReader = Task.Run(async () =>
            {
                Log.Info($"gameserver channel opened for user!");
                while (await requestStream.MoveNext(CancellationToken.None))
                {
                    try
                    {
                        var response = ProcessRequest(clientTokenValue, requestStream.Current);

                        if (response != null)
                        {
                            await responseStream.WriteAsync(response);
                        }
                    }
                    catch
                    {
                        ;
                    }

                }
            });

            await requestStreamReader;
            Log.Info($"gameserver channel closed for user!");
        }

        private GameServerStream ProcessRequest(string clientTokenValue, GameServerStream current)
        {
            switch (current.MessageType)
            {
                case MsgType.Initialisation:
                    return new GameServerStream() { MessageType = MsgType.Initialisation, MessageState = true, Message = string.Empty };
                case MsgType.Invitation:
                    Log.Debug($"Invitation reply received.");
                    goto case MsgType.InGame;
                case MsgType.InGame:
                    ProcessGameData(current.MessageType, current.MessageState, JsonConvert.DeserializeObject<GameData>(current.Message));
                    return null;
                default:
                    return new GameServerStream() { MessageType = MsgType.Initialisation, MessageState = false, Message = string.Empty };
            }
        }

        public override async Task GameStateChannel(GameStateStreamRequest gameStateStreamRequest, IServerStreamWriter<GameStateStream> responseStream, ServerCallContext context)
        {
            if (!TokenAuthentification(context.RequestHeaders, out string clientTokenValue))
            {
                return;
            }

            if (!_registredUsers.TryGetValue(clientTokenValue, out UserDataInfo userDataInfo))
            {
                Log.Info($"couldn't get user data info!");
                return;
            }

            //if (ClientManager.ClientDictionary.ContainsKey(clientTokenValue))
            //{
            //    Log.Info($"bad game server channel request, token already registred!");
            //    return;
            //}

            //if (!ClientManager.ClientDictionary.TryAdd(clientTokenValue,
            //    new Client(requestStream, responseStream, context)))
            //{
            //    Log.Info($"bad game server channel request, couldn't add to client manager!");
            //    return;
            //};

            // adding the response stream to user data
            //userDataInfo.ResponseStream = responseStream;

            var requestStreamReader = Task.Run(async () =>
            {
                Log.Info($"gamestate channel opened for user!");
                while (true)
                {
                    await responseStream.WriteAsync(new GameStateStream() { Message = $"tick tack .... game state {userDataInfo.UserState} !!!" });
                    Thread.Sleep(1000);
                }

            });

            await requestStreamReader;
            Log.Info($"game state channel closed for user!");
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

            if (!_registredUsers.TryGetValue(clientTokenValue, out UserDataInfo userDataInfo))
            {
                Log.Info($"couldn't get user data info!");
                return Task.FromResult(new ServerReply
                {
                    RequestState = false,
                    RequestMessage = string.Empty
                });
            }

            if (!ClientManager.ClientDictionary.TryGetValue(clientTokenValue, out Client _))
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

    public static class ClientManager
    {
        public static ConcurrentDictionary<string, Client> ClientDictionary = new ConcurrentDictionary<string, Client>();

        public static void SendMessage(string clientName, string message)
        {
            ClientDictionary[clientName].SendMessage(message);
        }

        public static void Disconnect(string clientName)
        {
            ClientDictionary[clientName].Disconnect();
        }
    }

    public class Client
    {
        private readonly IAsyncStreamReader<GameServerStream> _requestStream;
        private readonly IServerStreamWriter<GameServerStream> _responseStream;
        private readonly ServerCallContext _context;
        private readonly CancellationTokenSource _cts;

        private TaskCompletionSource<GameServerStream> _tcs;


        public CancellationToken CancellationToken => _cts.Token;

        public Client(IAsyncStreamReader<GameServerStream> requestStream, IServerStreamWriter<GameServerStream> responseStream, ServerCallContext context)
        {
            _requestStream = requestStream;
            _responseStream = responseStream;
            _context = context;
            _cts = new CancellationTokenSource();
            _tcs = new TaskCompletionSource<GameServerStream>();
        }

        public void Disconnect()
        {
            _tcs.SetCanceled();
            _cts.Cancel();
        }

        public void SendMessage(string message)
        {
            if (_tcs == null)
                throw new Exception();

            _tcs.SetResult(new GameServerStream
            {
                Message = message
            });
        }
    }


}