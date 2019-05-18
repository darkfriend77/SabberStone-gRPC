using System;
using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using log4net;
using SabberStoneContract.Model;

namespace SabberStoneServer.Services
{
    public class GameServerServiceImpl : GameServerService.GameServerServiceBase
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private int _index = 10000;
        public int NextSessionIndex => _index++;

        private readonly ConcurrentDictionary<string, UserInfo> _registredUsers;

        public GameServerServiceImpl()
        {
            _registredUsers = new ConcurrentDictionary<string, UserInfo>();
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

            // already authentificated accounts
            if (_registredUsers.ContainsKey(request.AccountName) )
            {
                Log.Warn($"{request.AccountName} is already registred!");
                return Task.FromResult(new AuthReply() {RequestState = false});
            }

            var userInfo = new UserInfo()
            {
                SessionId = NextSessionIndex,
                AccountName = request.AccountName,
                UserState = UserState.None,
                GameId = -1,
                DeckType = DeckType.None,
                DeckData = string.Empty,
                PlayerState = PlayerState.None
            };

            var token = Helper.ComputeSha256Hash(userInfo.SessionId + userInfo.AccountName);

            // failed registration
            if (!_registredUsers.TryAdd(token, userInfo))
            {
                Log.Warn($"failed to register user with account {request.AccountName}!");
                return Task.FromResult(new AuthReply() { RequestState = false });
            }

            var reply = new AuthReply
            {
                RequestState = true,
                RequestMessage = string.Empty,
                SessionId = userInfo.SessionId,
                SessionToken = token
            };

            return Task.FromResult(reply);
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