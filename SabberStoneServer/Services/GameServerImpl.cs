using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using log4net;
using SabberStoneContract.Model;
using SabberStoneServer.Core;

namespace SabberStoneServer.Services
{
    public class GameServerServiceImpl : GameServerService.GameServerServiceBase
    {
        private static readonly ILog Log = Logger.Instance.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private int _index = 10000;
        public int NextSessionIndex => _index++;

        private readonly ConcurrentDictionary<string, UserDataInfo> _registredUsers;

        public GameServerServiceImpl()
        {
            _registredUsers = new ConcurrentDictionary<string, UserDataInfo>();
        }

        public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
        {
            Log.Info(context.Peer);

            var reply = new PingReply
            {
                RequestState = true,
                RequestMessage = "Ping",
            };

            return Task.FromResult(reply);
        }

        public override Task<AuthReply> Authentication(AuthRequest request, ServerCallContext context)
        {

            Log.Info(context.Peer);

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
                PlayerState = PlayerState.None
            };


            // failed registration
            if (!_registredUsers.TryAdd(userInfo.Token, userInfo))
            {
                Log.Warn($"failed to register user with account {request.AccountName}!");
                return Task.FromResult(new AuthReply { RequestState = false });
            }

            var reply = new AuthReply
            {
                RequestState = true,
                RequestMessage = string.Empty,
                SessionId = userInfo.SessionId,
                SessionToken = userInfo.Token
            };

            return Task.FromResult(reply);
        }

        public override async Task GameServerChannel(IAsyncStreamReader<GameServerStream> requestStream, IServerStreamWriter<GameServerStream> responseStream, ServerCallContext context)
        {
            var requestStreamReader = Task.Run(async () =>
            {
                Log.Info($"gameserver channel opened for user!");
                while (await requestStream.MoveNext(CancellationToken.None))
                {
                    var response = ProcessRequest(requestStream.Current);
                    await responseStream.WriteAsync(response);
                }
            });


            await requestStreamReader;
            Log.Info($"gameserver channel closed for user!");
        }

        private GameServerStream ProcessRequest(GameServerStream current)
        {
            return new GameServerStream() {
                SessionId = current.SessionId,
                SessionToken = current.SessionToken,
                Message = $"{current.Message} ... ECHO"
            };
        }

        public override Task<QueueReply> GameQueue(QueueRequest request, ServerCallContext context)
        {
            Log.Info(context.Peer);

            var reply = new QueueReply
            {
                RequestState = true,
                RequestMessage = string.Empty
            };

            return Task.FromResult(reply);
        }
    }

    public class Helper
    {
        public static string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256   
            using (var sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string   
                var builder = new StringBuilder();
                foreach (var t in bytes)
                {
                    builder.Append(t.ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}