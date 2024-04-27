using AllLive.Core.Helper;
using AllLive.Core.Interface;
using AllLive.Core.Models;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
/*
* 哔哩哔哩弹幕实现
* 参考文档：https://github.com/lovelyyoshino/Bilibili-Live-API/blob/master/API.WebSocket.md
*/
namespace AllLive.Core.Danmaku
{

    public class BiliBiliDanmaku : ILiveDanmaku
    {
        private readonly Uri ServerUri;
        private readonly Timer HeartBeatTimer;
        private readonly ClientWebSocket WsClient;

        private int RoomId = 0;

        public int HeartbeatTime => 60 * 1000;

        public event EventHandler<LiveMessage> NewMessageEvent;
        public event EventHandler<string> CloseEvent;

        public BiliBiliDanmaku()
        {
            ServerUri = new Uri("wss://broadcastlv.chat.bilibili.com/sub");
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
                    ParseData(buffer.Array);
                }
                catch (Exception)
                {
                    break;
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

        public async Task Start(object args)
        {
            RoomId = args.ToInt32();
            await WsClient.ConnectAsync(ServerUri, default);
            if (WsClient.State == WebSocketState.Open)
            {
                string token = "";
                try
                {
                    var result = await HttpUtil.GetString($"https://api.live.bilibili.com/room/v1/Danmu/getConf?room_id={RoomId}&platform=pc&player=web");
                    token = JsonNode.Parse(result)["data"]["token"].ToString();
                }
                catch (Exception) { }
                //发送进房信息
                var data = EncodeData(JsonSerializer.Serialize(new
                {
                    uid = 0,
                    roomid = RoomId,
                    protover = 2,
                    buvid = System.Guid.NewGuid().ToString(),
                    platform = "web",
                    type = 2,
                    key = token
                }), 7);
                await WsClient.SendAsync(data, WebSocketMessageType.Binary, true, default);
                HeartBeatTimer.Start();
                ReceiveMessage();
            }
            else
            {
                OnClose();
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
                await WsClient.SendAsync(EncodeData("", 2), WebSocketMessageType.Binary, true, default);
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

        private void ParseData(byte[] data)
        {
            //协议版本。0为JSON，可以直接解析；1为房间人气值,Body为4位Int32；2为压缩过Buffer，需要解压再处理
            int protocolVersion = BitConverter.ToInt32(new byte[4] { data[7], data[6], 0, 0 }, 0);
            //操作类型。3=心跳回应，内容为房间人气值；5=通知，弹幕、广播等全部信息；8=进房回应，空
            int operation = BitConverter.ToInt32(data.Skip(8).Take(4).Reverse().ToArray(), 0);
            //内容
            var body = data.Skip(16).ToArray();
            if (operation == 3)
            {
                var online = BitConverter.ToInt32(body.Reverse().ToArray(), 0);
                NewMessageEvent?.Invoke(this, new LiveMessage()
                {
                    Data = online,
                    Type = LiveMessageType.Online,
                });
            }
            else if (operation == 5)
            {
                if (protocolVersion == 2)
                {
                    body = DecompressData(body);
                }
                var text = Encoding.UTF8.GetString(body);
                //可能有多条数据，做个分割
                var textLines = Regex.Split(text, "[\x00-\x1f]+").Where(x => x.Length > 2 && x[0] == '{').ToArray();
                foreach (var item in textLines)
                {
                    ParseMessage(item);
                }
            }
        }

        private void ParseMessage(string jsonMessage)
        {
            try
            {
                var obj = JsonNode.Parse(jsonMessage);
                var cmd = obj["cmd"].ToString();
                if (cmd.Contains("DANMU_MSG"))
                {
                    if (obj["info"] != null && obj["info"].AsArray().Count != 0)
                    {
                        var message = obj["info"][1].ToString();
                        var color = obj["info"][0][3].ToInt32();
                        if (obj["info"][2] != null && obj["info"][2].AsArray().Count != 0)
                        {
                            var username = obj["info"][2][1].ToString();
                            NewMessageEvent?.Invoke(this, new LiveMessage()
                            {
                                Type = LiveMessageType.Chat,
                                Message = message,
                                UserName = username,
                                Color = color == 0 ? Color.White : Utils.NumberToColor(color),
                            });
                        }
                    }
                }
            }
            catch (Exception)
            {

            }
        }

        /// <summary>
        /// 对数据进行编码
        /// </summary>
        /// <param name="msg">文本内容</param>
        /// <param name="action">2=心跳，7=进房</param>
        /// <returns></returns>
        private ArraySegment<byte> EncodeData(string msg, int action)
        {
            var data = Encoding.UTF8.GetBytes(msg);
            //头部长度固定16
            var length = data.Length + 16;
            var buffer = new byte[length];
            using (var ms = new MemoryStream(buffer))
            {

                //数据包长度
                var b = BitConverter.GetBytes(buffer.Length).ToArray().Reverse().ToArray();
                ms.Write(b, 0, 4);
                //数据包头部长度,固定16
                b = BitConverter.GetBytes(16).Reverse().ToArray();
                ms.Write(b, 2, 2);
                //协议版本，0=JSON,1=Int32,2=Buffer
                b = BitConverter.GetBytes(0).Reverse().ToArray(); ;
                ms.Write(b, 0, 2);
                //操作类型
                b = BitConverter.GetBytes(action).Reverse().ToArray(); ;
                ms.Write(b, 0, 4);
                //数据包头部长度,固定1
                b = BitConverter.GetBytes(1).Reverse().ToArray(); ;
                ms.Write(b, 0, 4);
                //数据
                ms.Write(data, 0, data.Length);
                var _bytes = ms.ToArray();
                ms.Flush();
                return new ArraySegment<byte>(_bytes);
            }
        }

        /// <summary>
        /// 解码数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private byte[] DecompressData(byte[] data)
        {
            using (MemoryStream outBuffer = new MemoryStream())
            using (System.IO.Compression.DeflateStream compressedzipStream = new System.IO.Compression.DeflateStream(new MemoryStream(data, 2, data.Length - 2), System.IO.Compression.CompressionMode.Decompress))
            {
                byte[] block = new byte[1024];
                while (true)
                {
                    int bytesRead = compressedzipStream.Read(block, 0, block.Length);
                    if (bytesRead <= 0)
                        break;
                    else
                        outBuffer.Write(block, 0, bytesRead);
                }
                compressedzipStream.Close();
                return outBuffer.ToArray();
            }
        }
    }
}
