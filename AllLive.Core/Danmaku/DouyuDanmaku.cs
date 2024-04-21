using AllLive.Core.Helper;
using AllLive.Core.Interface;
using AllLive.Core.Models;
using System;
using System.Drawing;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Timers;
/*
* 斗鱼弹幕实现
* 参考项目：
* https://github.com/IsoaSFlus/danmaku
* https://www.cnblogs.com/sdflysha/p/20210117-douyu-barrage-with-dotnet.html
* 
* 斗鱼如果使用System.Net.WebSockets.ClientWebSocket,30秒后会被关闭连接...
*/

namespace AllLive.Core.Danmaku
{
    public class DouyuDanmaku : ILiveDanmaku
    {
        private readonly Uri ServerUri;
        private readonly Timer HeartBeatTimer;
        private readonly ClientWebSocket WsClient;

        private string roomId;

        public int HeartbeatTime => 45 * 1000;

        public event EventHandler<LiveMessage> NewMessageEvent;
        public event EventHandler<string> CloseEvent;

        public DouyuDanmaku()
        {
            ServerUri = new Uri("wss://danmuproxy.douyu.com:8506");
            WsClient = new ClientWebSocket();
            HeartBeatTimer = new Timer(HeartbeatTime);
            HeartBeatTimer.Elapsed += Timer_Elapsed;
        }

        private async void ReceiveMessage()
        {
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[1024]);
            while (WsClient.State == WebSocketState.Open)
            {
                try
                {
                    WebSocketReceiveResult result = await WsClient.ReceiveAsync(buffer, default);
                    string message = DeserializeDouyu(buffer.Array);
                    if (message.Length != 0)
                    {
                        var json = SttToJObject(message);
                        var type = json["type"]?.ToString();
                        //斗鱼好像不会返回人气值
                        //有些直播间存在阴间弹幕，不知道什么情况
                        if (type == "chatmsg")
                        {
                            NewMessageEvent?.Invoke(this, new LiveMessage()
                            {
                                UserName = json["nn"].ToString(),
                                Message = json["txt"].ToString(),
                                Color = GetColor(json["col"].ToInt32())
                            });
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

        private Color GetColor(int type)
        {
            switch (type)
            {
                case 1:
                    return Color.Red;
                case 2:
                    return Color.FromArgb(30, 135, 240);
                case 3:
                    return Color.FromArgb(122, 200, 75);
                case 4:
                    return Color.FromArgb(255, 127, 0);
                case 5:
                    return Color.FromArgb(155, 57, 244);
                case 6:
                    return Color.FromArgb(255, 105, 180);
                default:
                    return Color.White;
            }
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Heartbeat();
        }

        public async void Heartbeat()
        {
            if (WsClient.State == WebSocketState.Open)
            {
                await WsClient.SendAsync(SerializeDouyu($"type@=mrkl/"), WebSocketMessageType.Binary, true, default);
            }
        }

        public async Task Start(object args)
        {
            this.roomId = args.ToString();
            await WsClient.ConnectAsync(ServerUri, default);
            if (WsClient.State == WebSocketState.Open)
            {
                //发送进房信息
                await WsClient.SendAsync(SerializeDouyu($"type@=loginreq/roomid@={roomId}/"), WebSocketMessageType.Binary, true, default);
                await WsClient.SendAsync(SerializeDouyu($"type@=joingroup/rid@={roomId}/gid@=-9999/"), WebSocketMessageType.Binary, true, default);
                HeartBeatTimer.Start();
                ReceiveMessage();
            }
            else
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

        private void OnClose()
        {
            CloseEvent?.Invoke(this, WsClient.State.ToString());
        }

        private ArraySegment<byte> SerializeDouyu(string body)
        {
            const short ClientSendToServer = 689;
            const byte Encrypted = 0;
            const byte Reserved = 0;

            byte[] bodyBuffer = Encoding.UTF8.GetBytes(body);
            using (var ms = new MemoryStream(bodyBuffer.Length + 13))
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write(4 + 4 + body.Length + 1);
                    writer.Write(4 + 4 + body.Length + 1);
                    writer.Write(ClientSendToServer);
                    writer.Write(Encrypted);
                    writer.Write(Reserved);
                    writer.Write(bodyBuffer);
                    writer.Write((byte)0);
                    writer.Flush();
                    return new ArraySegment<byte>(ms.ToArray());
                }
            }
        }

        private string DeserializeDouyu(byte[] bytes)
        {
            try
            {
                using (var ms = new MemoryStream(bytes, 0, bytes.Length, writable: false))

                using (var reader = new BinaryReader(ms))
                {
                    int fullMsgLength = reader.ReadInt32();
                    int fullMsgLength2 = reader.ReadInt32();

                    int bodyLength = fullMsgLength - 1 - 4 - 4;
                    short packType = reader.ReadInt16();
                    short encrypted = reader.ReadByte();
                    short reserved = reader.ReadByte();
                    var _bytes = reader.ReadBytes(bodyLength);
                    byte zero = reader.ReadByte();
                    return Encoding.UTF8.GetString(_bytes);
                }
            }
            catch (Exception)
            {
                return "";
            }
        }

        //辣鸡STT
        private JsonNode SttToJObject(string str)
        {
            if (str.Contains("//"))
            {
                var result = new JsonArray();
                foreach (var field in str.Split(new[] { "//" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    result.Add(SttToJObject(field));
                }
                return result;
            }
            if (str.Contains("@="))
            {
                var result = new JsonObject();
                foreach (var field in str.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var tokens = field.Split(new[] { "@=" }, StringSplitOptions.None);
                    var k = tokens[0];
                    var v = UnscapeSlashAt(tokens[1]);
                    result[k] = SttToJObject(v);
                }
                return result;
            }
            else if (str.Contains("@A="))
            {
                return SttToJObject(UnscapeSlashAt(str));
            }
            else
            {
                return UnscapeSlashAt(str);
            }


        }
        private string UnscapeSlashAt(string str)
        {
            return str
                .Replace("@S", "/")
                .Replace("@A", "@");
        }

    }
}
