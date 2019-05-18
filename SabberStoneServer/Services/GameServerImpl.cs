using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading.Tasks;
using Grpc.Core;
using log4net;

namespace SabberStoneServer.Services
{
    public class GameServerServiceImpl : GameServerService.GameServerServiceBase
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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

            var reply = new AuthReply
            {
                RequestState = true,
                RequestMessage = string.Empty,
                SessionId = 1,
                SessionToken = ""
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
}