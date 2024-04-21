using AllLive.Core.Interface;
using AllLive.Core.Models;
using System;
using System.Threading.Tasks;

namespace AllLive.Core.Danmaku
{
    public class EgameDanmaku : ILiveDanmaku
    {
        public int HeartbeatTime => 60;

        public event EventHandler<LiveMessage> NewMessageEvent;
        public event EventHandler<string> CloseEvent;

        public void Heartbeat()
        {
            throw new NotImplementedException();
        }

        public Task Start(object args)
        {
            throw new NotImplementedException();
        }

        public Task Stop()
        {
            throw new NotImplementedException();
        }
    }
}
