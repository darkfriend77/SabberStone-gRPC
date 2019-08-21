using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Grpc.Core;
using static GameServerService;

namespace SabberStoneContract.Client
{
    public class SmartClient : GameServerServiceClient
    {
        public SmartClient(Channel channel) : base(channel)
        {
        }

        public override AsyncDuplexStreamingCall<GameServerStream, GameServerStream> GameServerChannel(Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default)
        {
            return base.GameServerChannel(headers, deadline, cancellationToken);
        }
    }
}
