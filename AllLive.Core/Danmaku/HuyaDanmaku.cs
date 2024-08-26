using AllLive.Core.Helper;
using AllLive.Core.Interface;
using AllLive.Core.Models;
using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Timers;
using Tup.Tars;
/*
* 虎牙弹幕实现
* 参考项目：
* https://github.com/BacooTang/huya-danmu
* https://github.com/IsoaSFlus/danmaku
*/
namespace AllLive.Core.Danmaku
{
    public class HuyaDanmakuArgs
    {
        public HuyaDanmakuArgs(long ayyuid, long topSid, long subSid)
        {
            this.Ayyuid = ayyuid;
            this.SubSid = subSid;
            this.TopSid = topSid;
        }
        public long Ayyuid { get; set; }
        public long TopSid { get; set; }
        public long SubSid { get; set; }
    }
    public class HuyaDanmaku : ILiveDanmaku
    {
        private readonly Uri ServerUri;
        private readonly Timer HeartBeatTimer;
        private readonly ClientWebSocket WsClient;
        private readonly System.Threading.Thread ReceiveThread;
        private readonly byte[] HeartBeatData;

        private HuyaDanmakuArgs DanmakuArgs;

        public int HeartbeatTime => 60 * 1000;
        public event EventHandler<LiveMessage> NewMessageEvent;
        public event EventHandler<string> CloseEvent;

        public HuyaDanmaku()
        {
            ServerUri = new Uri("wss://cdnws.api.huya.com");
            WsClient = new ClientWebSocket();
            ReceiveThread = new System.Threading.Thread(ReceiveMessage);
            HeartBeatData = Convert.FromBase64String("ABQdAAwsNgBM");
            HeartBeatTimer = new Timer(HeartbeatTime);
            HeartBeatTimer.Elapsed += Timer_Elapsed;
        }

        private void ReceiveMessage()
        {
            var buffer = new byte[4096];
            while (WsClient.State == WebSocketState.Open)
            {
                try
                {
                    WsClient.ReceiveAsync(new ArraySegment<byte>(buffer), default).Wait();
                    var stream = new TarsInputStream(buffer);
                    var type = stream.Read(0, 0, false);
                    if (type == 7)
                    {
                        stream = new TarsInputStream(stream.Read(new byte[0], 1, false));
                        HYPushMessage wSPushMessage = new HYPushMessage();
                        wSPushMessage.ReadFrom(stream);
                        if (wSPushMessage.Uri == 1400)
                        {

                            HYMessage messageNotice = new HYMessage();
                            messageNotice.ReadFrom(new TarsInputStream(wSPushMessage.Msg));
                            var uname = messageNotice.UserInfo.NickName;
                            var content = messageNotice.Content;
                            var color = messageNotice.BulletFormat.FontColor;
                            NewMessageEvent?.Invoke(this, new LiveMessage()
                            {
                                Type = LiveMessageType.Chat,
                                Message = content,
                                UserName = uname,
                                Color = color <= 0 ? DanmakuColor.White : new DanmakuColor(color),
                            });

                        }
                        if (wSPushMessage.Uri == 8006)
                        {
                            long online = 0;
                            var s = new TarsInputStream(wSPushMessage.Msg);
                            online = s.Read(online, 0, false);
                            NewMessageEvent?.Invoke(this, new LiveMessage()
                            {
                                Type = LiveMessageType.Online,
                                Data = online,
                            });
                        }
                    }
                    else if (type == 22)
                    {
                        Debug.WriteLine($"收到消息:[Type:{type}]");
                        stream = new TarsInputStream(stream.Read(new byte[0], 1, false));
                        HYPushMessageV2 wSPushMessage = new HYPushMessageV2();
                        wSPushMessage.ReadFrom(stream);
                        foreach (var item in wSPushMessage.MsgItem)
                        {
                            if (item.Uri == 1400)
                            {
                                HYMessage messageNotice = new HYMessage();
                                messageNotice.ReadFrom(new TarsInputStream(item.Msg));
                                var uname = messageNotice.UserInfo.NickName;
                                var content = messageNotice.Content;
                                var color = messageNotice.BulletFormat.FontColor;
                                NewMessageEvent?.Invoke(this, new LiveMessage()
                                {
                                    Type = LiveMessageType.Chat,
                                    Message = content,
                                    UserName = uname,
                                    Color = color <= 0 ? DanmakuColor.White : new DanmakuColor(color),
                                });

                            }
                            if (item.Uri == 8006)
                            {
                                long online = 0;
                                var s = new TarsInputStream(item.Msg);
                                online = s.Read(online, 0, false);
                                NewMessageEvent?.Invoke(this, new LiveMessage()
                                {
                                    Type = LiveMessageType.Online,
                                    Data = online,
                                });
                            }
                        }

                    }
                }
                catch (Exception)
                {
                }
            }
            if (WsClient.State != WebSocketState.Open)
            {
                OnClose();
            }
        }

        private void OnClose()
        {
            CloseEvent?.Invoke(this, WsClient.State.ToString());
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Heartbeat();
        }

        public async void Heartbeat()
        {
            if (WsClient.State == WebSocketState.Open)
            {
                await WsClient.SendAsync(new ArraySegment<byte>(HeartBeatData), WebSocketMessageType.Binary, true, default);
            }
        }

        public async Task Start(object args)
        {
            DanmakuArgs = (HuyaDanmakuArgs)args;
            try
            {
                await WsClient.ConnectAsync(ServerUri, default);
                if (WsClient.State == WebSocketState.Open)
                {
                    //发送进房信息
                    await WsClient.SendAsync(JoinData(DanmakuArgs.Ayyuid, DanmakuArgs.TopSid, DanmakuArgs.SubSid), WebSocketMessageType.Binary, true, default);
                    HeartBeatTimer.Start();
                    ReceiveThread.Start();
                    //ReceiveMessage();
                }
                else
                {
                    OnClose();
                }
            }
            catch (Exception)
            {
                OnClose();
            }
        }

        public async Task Stop()
        {
            if (WsClient.State == WebSocketState.Connecting)
            {
                WsClient.Abort();
            }
            if (WsClient.State == WebSocketState.Open)
            {
                await WsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", default);
            }
            HeartBeatTimer.Stop();
        }

        private ArraySegment<byte> JoinData(long ayyuid, long tid, long sid)
        {
            var oos = new TarsOutputStream();
            oos.Write(ayyuid, 0);
            oos.Write(true, 1);
            oos.Write("", 2);
            oos.Write("", 3);
            oos.Write(tid, 4);
            oos.Write(sid, 5);
            oos.Write(0, 6);
            oos.Write(0, 7);

            var wscmd = new TarsOutputStream();
            wscmd.Write(1, 0);
            wscmd.Write(oos.toByteArray(), 1);
            return new ArraySegment<byte>(wscmd.toByteArray());
        }
    }

    public class HYPushMessage : TarsStruct
    {
        public int PushType = 0;
        public long Uri = 0;
        public byte[] Msg = new byte[0];
        public int ProtocolType = 0;
        public override void ReadFrom(TarsInputStream _is)
        {
            PushType = _is.Read(PushType, 0, false);
            Uri = _is.Read(Uri, 1, false);
            Msg = _is.Read(Msg, 2, false);
            ProtocolType = _is.Read(ProtocolType, 3, false);
        }

        public override void WriteTo(TarsOutputStream _os)
        {
            _os.Write(PushType, 0);
            _os.Write(Uri, 1);
            _os.Write(Msg, 2);
            _os.Write(ProtocolType, 3);
        }
    }
    public class HYPushMessageV2 : TarsStruct
    {


        public string GroupId = "";
        public HYMsgItem[] MsgItem = new HYMsgItem[] { };
        public int ProtocolType = 0;
        public override void ReadFrom(TarsInputStream _is)
        {
            GroupId = _is.Read(GroupId, 0, false);
            MsgItem = _is.readArray<HYMsgItem>(MsgItem, 1, false);
        }

        public override void WriteTo(TarsOutputStream _os)
        {
            _os.Write(GroupId, 0);
            _os.Write(MsgItem, 1);
        }
    }
    public class HYMsgItem : TarsStruct
    {
        public long Uri = 0;
        public byte[] Msg = new byte[0];
        public long MsgId = 0;
        public override void ReadFrom(TarsInputStream _is)
        {
            Uri = _is.Read(Uri, 0, false);
            Msg = _is.Read(Msg, 1, false);
            MsgId = _is.Read(MsgId, 2, false);
        }

        public override void WriteTo(TarsOutputStream _os)
        {
            _os.Write(Uri, 0);
            _os.Write(Msg, 1);
            _os.Write(MsgId, 2);
        }
    }
    public class HYSender : TarsStruct
    {
        public long Uid = 0;
        public long Lmid = 0;
        public string NickName = "";
        public int Gender = 0;

        public override void ReadFrom(TarsInputStream _is)
        {
            Uid = _is.Read(Uid, 0, false);
            Lmid = _is.Read(Lmid, 0, false);
            NickName = _is.Read(NickName, 2, false);
            Gender = _is.Read(Gender, 3, false);
        }

        public override void WriteTo(TarsOutputStream _os)
        {
            _os.Write(Uid, 0);
            _os.Write(Lmid, 1);
            _os.Write(NickName, 2);
            _os.Write(Gender, 3);
        }
    }
    public class HYMessage : TarsStruct
    {
        public HYSender UserInfo = new HYSender();
        public string Content = "";
        public HYBulletFormat BulletFormat = new HYBulletFormat();
        public override void ReadFrom(TarsInputStream _is)
        {
            UserInfo = (HYSender)_is.Read(UserInfo, 0, false);
            Content = _is.Read(Content, 3, false);
            BulletFormat = (HYBulletFormat)_is.Read(BulletFormat, 6, false);
        }
        public override void WriteTo(TarsOutputStream _os)
        {
            _os.Write(UserInfo, 0);
            _os.Write(Content, 3);
            _os.Write(BulletFormat, 6);
        }
    }
    public class HYBulletFormat : TarsStruct
    {
        public int FontColor = 0;
        public int FontSize = 4;
        public int TextSpeed = 0;
        public int TransitionType = 1;
        public override void ReadFrom(TarsInputStream _is)
        {
            FontColor = _is.Read(FontColor, 0, false);
            FontSize = _is.Read(FontSize, 1, false);
            TextSpeed = _is.Read(TextSpeed, 2, false);
            TransitionType = _is.Read(TransitionType, 3, false);
        }
        public override void WriteTo(TarsOutputStream _os)
        {
            _os.Write(FontColor, 0);
            _os.Write(FontSize, 1);
            _os.Write(FontSize, 2);
            _os.Write(FontSize, 3);
        }
    }
}
