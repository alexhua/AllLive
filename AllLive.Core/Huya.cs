﻿using AllLive.Core.Interface;
using AllLive.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AllLive.Core.Danmaku;
using AllLive.Core.Helper;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;
using System.Net.WebSockets;
using System.Web;
using WebSocketSharp;
using System.Collections.Specialized;

namespace AllLive.Core
{
    public class Huya : ILiveSite
    {
        public string Name => "虎牙直播";
        public ILiveDanmaku GetDanmaku() => new HuyaDanmaku();
        public async Task<List<LiveCategory>> GetCategores()
        {
            List<LiveCategory> categories = new List<LiveCategory>() {
                new LiveCategory() {
                    ID="1",
                    Name="网游",
                },
                new LiveCategory() {
                    ID="2",
                    Name="单机",
                },
                new LiveCategory() {
                    ID="8",
                    Name="娱乐",
                },
                new LiveCategory() {
                    ID="3",
                    Name="手游",
                },
            };
            foreach (var item in categories)
            {
                item.Children = await GetSubCategories(item.ID);
            }
            return categories;
        }

        private async Task<List<LiveSubCategory>> GetSubCategories(string id)
        {
            List<LiveSubCategory> subs = new List<LiveSubCategory>();
            var result = await HttpUtil.GetString($"https://live.cdn.huya.com/liveconfig/game/bussLive?bussType={id}");
            var obj = JObject.Parse(result);
            foreach (var item in obj["data"])
            {
                subs.Add(new LiveSubCategory()
                {
                    Pic = $"https://huyaimg.msstatic.com/cdnimage/game/{item["gid"].ToString()}-MS.jpg",
                    ID = item["gid"].ToString(),
                    ParentID = id,
                    Name = item["gameFullName"].ToString(),
                });
            }
            return subs;
        }
        public async Task<LiveCategoryResult> GetCategoryRooms(LiveSubCategory category, int page = 1)
        {
            LiveCategoryResult categoryResult = new LiveCategoryResult()
            {
                Rooms = new List<LiveRoomItem>(),

            };
            var result = await HttpUtil.GetString($"https://www.huya.com/cache.php?m=LiveList&do=getLiveListByPage&tagAll=0&gameId={category.ID}&page={page}");
            var obj = JObject.Parse(result);
            foreach (var item in obj["data"]["datas"])
            {
                var cover = item["screenshot"].ToString();
                if (!cover.Contains("?"))
                {
                    cover += "?x-oss-process=style/w338_h190&";
                }
                var title = item["roomName"]?.ToString();
                if (string.IsNullOrEmpty(title))
                {
                    title = item["introduction"]?.ToString() ?? "";
                }
                categoryResult.Rooms.Add(new LiveRoomItem()
                {
                    Cover = cover,
                    Online = item["totalCount"].ToInt32(),
                    RoomID = item["profileRoom"].ToString(),
                    Title = title,
                    UserName = item["nick"].ToString(),
                });
            }
            categoryResult.HasMore = obj["data"]["page"].ToInt32() < obj["data"]["totalPage"].ToInt32();
            return categoryResult;
        }
        public async Task<LiveCategoryResult> GetRecommendRooms(int page = 1)
        {
            LiveCategoryResult categoryResult = new LiveCategoryResult()
            {
                Rooms = new List<LiveRoomItem>(),

            };
            var result = await HttpUtil.GetString($"https://www.huya.com/cache.php?m=LiveList&do=getLiveListByPage&tagAll=0&page={page}");
            var obj = JObject.Parse(result);

            foreach (var item in obj["data"]["datas"])
            {
                var cover = item["screenshot"].ToString();
                if (!cover.Contains("?"))
                {
                    cover += "?x-oss-process=style/w338_h190&";
                }
                var title = item["roomName"]?.ToString();
                if (string.IsNullOrEmpty(title))
                {
                    title = item["introduction"]?.ToString() ?? "";
                }
                categoryResult.Rooms.Add(new LiveRoomItem()
                {
                    Cover = cover,
                    Online = item["totalCount"].ToInt32(),
                    RoomID = item["profileRoom"].ToString(),
                    Title = title,
                    UserName = item["nick"].ToString(),
                });
            }
            categoryResult.HasMore = obj["data"]["page"].ToInt32() < obj["data"]["totalPage"].ToInt32();
            return categoryResult;
        }
        public async Task<LiveRoomDetail> GetRoomDetail(object roomId)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("user-agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 13_2_3 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/13.0.3 Mobile/15E148 Safari/604.1 Edg/91.0.4472.69");
            var result = await HttpUtil.GetString($"https://m.huya.com/{roomId}", headers);
            var jsonStr = Regex.Match(result, @"window\.HNF_GLOBAL_INIT.=.\{(.*?)\}.</script>", RegexOptions.Singleline).Groups[1].Value;
            var jsonObj = JObject.Parse($"{{{jsonStr}}}");

            var title = jsonObj["roomInfo"]["tLiveInfo"]["sRoomName"].ToString();
            if (string.IsNullOrEmpty(title))
            {
                title = jsonObj["roomInfo"]["tLiveInfo"]["sIntroduction"].ToString();
            }

            var uid =await GetUid();
            var uuid = GetUuid();
            var huyaLines = new List<HuyaLineModel>();
            var huyaBiterates = new List<HuyaBitRateModel>();
            //读取可用线路
            var lines = jsonObj["roomInfo"]["tLiveInfo"]["tLiveStreamInfo"]["vStreamInfo"]["value"];
            foreach (var item in lines)
            {
                
                if ( !string.IsNullOrEmpty(item["sFlvUrl"]?.ToString()))
                {
                    huyaLines.Add(new HuyaLineModel()
                    {
                        Line = item["sFlvUrl"].ToString(),
                        LineType = HuyaLineType.FLV,
                        FlvAntiCode = item["sFlvAntiCode"].ToString(),
                        HlsAntiCode = item["sHlsAntiCode"].ToString(),
                        StreamName =  item["sStreamName"].ToString(),
                    });
                }
                //HLS效果不好，暂不使用
                //if (!string.IsNullOrEmpty(item["sHlsUrl"]?.ToString()))
                //{
                //    huyaLines.Add(new HuyaLineModel()
                //    {
                //        Line = item["sHlsUrl"].ToString().Replace("http://", "").Replace("https://", ""),
                //        LineType = HuyaLineType.HLS,
                //    });
                //}
            }
            //优先FLV
            //huyaLines=huyaLines.Where(x=>!x.Line.Contains("-game")).OrderBy(x=>x.LineType).ToList();

            //清晰度
            var biterates = jsonObj["roomInfo"]["tLiveInfo"]["tLiveStreamInfo"]["vBitRateInfo"]["value"];
            foreach (var item in biterates)
            {
                huyaBiterates.Add(new HuyaBitRateModel()
                {
                    BitRate = item["iBitRate"].ToInt32(),
                    Name = item["sDisplayName"].ToString(),
                });
            }
            return new LiveRoomDetail()
            {
                Cover = jsonObj["roomInfo"]["tLiveInfo"]["sScreenshot"].ToString(),
                Online = jsonObj["roomInfo"]["tLiveInfo"]["lTotalCount"].ToInt32(),
                RoomID = jsonObj["roomInfo"]["tLiveInfo"]["lProfileRoom"].ToString(),
                Title = title,
                UserName = jsonObj["roomInfo"]["tProfileInfo"]["sNick"].ToString(),
                UserAvatar = jsonObj["roomInfo"]["tProfileInfo"]["sAvatar180"].ToString(),
                Introduction = jsonObj["roomInfo"]["tLiveInfo"]["sIntroduction"].ToString(),
                Notice = jsonObj["welcomeText"].ToString(),
                Status = jsonObj["roomInfo"]["eLiveStatus"].ToInt32() == 2,
                Data = new HuyaUrlDataModel()
                {
                    Url = "https:" + Encoding.UTF8.GetString(Convert.FromBase64String(jsonObj["roomProfile"]["liveLineUrl"].ToString())),
                    Lines = huyaLines,
                    BitRates = huyaBiterates,
                    Uid=uid,
                    UUid=uuid,
                },
                DanmakuData = new HuyaDanmakuArgs(
                    jsonObj["roomInfo"]["tLiveInfo"]["lYyid"].ToInt64(),
                    result.MatchText(@"lChannelId"":([0-9]+)").ToInt64(),
                    result.MatchText(@"lSubChannelId"":([0-9]+)").ToInt64()
                ),
                Url = "https://www.huya.com/" + roomId
            };
        }
        private long GetUuid()
        {
            return (long)((DateTimeOffset.Now.ToUnixTimeMilliseconds() % 10000000000 * 1000 + (1000 * new Random().Next(0, int.MaxValue))) % uint.MaxValue);
        }
        private async Task<string> GetUid()
        {
            var data = "{\"appId\":5002,\"byPass\":3,\"context\":\"\",\"version\":\"2.4\",\"data\":{}}";
            var result = await HttpUtil.PostJsonString($"https://udblgn.huya.com/web/anonymousLogin",data);
            var obj = JObject.Parse(result);

            return obj["data"]["uid"].ToString();
        }

        public async Task<LiveSearchResult> Search(string keyword, int page = 1)
        {
            LiveSearchResult searchResult = new LiveSearchResult()
            {
                Rooms = new List<LiveRoomItem>(),

            };
            var result = await HttpUtil.GetUtf8String($"https://search.cdn.huya.com/?m=Search&do=getSearchContent&q={Uri.EscapeDataString(keyword)}&uid=0&v=4&typ=-5&livestate=0&rows=20&start={(page - 1) * 20}");
            var obj = JObject.Parse(result);

            foreach (var item in obj["response"]["3"]["docs"])
            {
                var cover = item["game_screenshot"].ToString();
                if (!cover.Contains("?"))
                {
                    cover += "?x-oss-process=style/w338_h190&";
                }
                searchResult.Rooms.Add(new LiveRoomItem()
                {
                    Cover = cover,
                    Online = item["game_total_count"].ToInt32(),
                    RoomID = item["room_id"].ToString(),
                    Title = item["game_roomName"].ToString(),
                    UserName = item["game_nick"].ToString(),
                });
            }
            searchResult.HasMore = obj["response"]["3"]["numFound"].ToInt32() > (page * 20);
            return searchResult;
        }
        public Task<List<LivePlayQuality>> GetPlayQuality(LiveRoomDetail roomDetail)
        {
            List<LivePlayQuality> qualities = new List<LivePlayQuality>();
            var urlData = roomDetail.Data as HuyaUrlDataModel;
            if (urlData.BitRates.Count ==0)
            {
                urlData.BitRates = new List<HuyaBitRateModel>() { 
                    new HuyaBitRateModel()
                    {
                        Name="原画",
                        BitRate=0,
                    },
                    new HuyaBitRateModel()
                    {
                        Name="高清",
                        BitRate=2000
                    },
                };
            }
            //if (urlData.Lines.Count == 0)
            //{
            //    urlData.Lines = new List<HuyaLineModel>() {
            //        new HuyaLineModel()
            //        {
            //            Line="tx.flv.huya.com",
            //            LineType= HuyaLineType.FLV
            //        },
            //        new HuyaLineModel()
            //        {
            //            Line="bd.flv.huya.com",
            //            LineType= HuyaLineType.FLV
            //        },
            //        new HuyaLineModel()
            //        {
            //            Line="al.flv.huya.com",
            //            LineType= HuyaLineType.FLV
            //        },
            //        new HuyaLineModel()
            //        {
            //            Line="hw.flv.huya.com",
            //            LineType= HuyaLineType.FLV
            //        },
            //    };
            //}
            //var url = GetRealUrl(urlData.Url);

            foreach (var item in urlData.BitRates)
            {
                var urls = new List<string>();
                foreach (var line in urlData.Lines)
                {
                    var src = line.Line;
                   
                    src += $"/{line.StreamName}";
                    if (line.LineType== HuyaLineType.FLV)
                    {
                        src += ".flv";
                    }
                    if (line.LineType == HuyaLineType.HLS)
                    {
                        src += ".m3u8";
                    }
                    
                    var param = ProcessAnticode(line.LineType == HuyaLineType.FLV?line.FlvAntiCode: line.HlsAntiCode, urlData.Uid, line.StreamName);

                    src += $"?{param}";

                    if (item.BitRate > 0)
                    {
                        src = $"{src}&ratio={item.BitRate}";
                    }
                    urls.Add(src);
                }
                qualities.Add(new LivePlayQuality() { 
                    Data = urls,
                    Quality=item.Name,
                });
            }

            


            return Task.FromResult(qualities);
        }
        public string ProcessAnticode(string anticode, string uid, string streamname)
        {
            // https://github.com/iceking2nd/real-url/blob/master/huya.py
            var query = HttpUtility.ParseQueryString(anticode);
            query["t"] = "100";
            query["ctype"] = "huya_live";
            var wsTime = (Utils.GetTimestamp() + 21600).ToString("x");
            var seqId =(Utils.GetTimestampMs() + long.Parse(uid)).ToString();
            var fm = Encoding.UTF8.GetString(Convert.FromBase64String(Uri.UnescapeDataString(query["fm"])));
            var wsSecretPrefix = fm.Split('_').First();
            var wsSecretHash = Utils.ToMD5($"{seqId}|{query["ctype"]}|{query["t"]}");
            var wsSecret = Utils.ToMD5($"{wsSecretPrefix}_{uid}_{streamname}_{wsSecretHash}_{wsTime}");


            var map = new NameValueCollection();
            map.Add("wsSecret", wsSecret);
            map.Add("wsTime", wsTime);
            map.Add("seqid", seqId);
            map.Add("ctype", query["ctype"]);
            map.Add("ver", "1");
            map.Add("fs", query["fs"]);
            map.Add("sphdcdn", query["sphdcdn"] ?? "");
            map.Add("sphdDC", query["sphdDC"] ?? "");
            map.Add("sphd", query["sphd"] ?? "");
            map.Add("exsphd", query["exsphd"] ?? "");
            map.Add("uid", uid);
            map.Add("uuid", GetUuid().ToString());
            map.Add("t", query["t"]);
            map.Add("sv", "2110211124");
          
            //将map转为字符串
            var param = string.Join("&", map.AllKeys.Select(x => $"{x}={map[x]}"));
            return param;
        }
       
        public Task<List<string>> GetPlayUrls(LiveRoomDetail roomDetail, LivePlayQuality qn)
        {
            return Task.FromResult(qn.Data as List<string>);
        }
        
    }
    public class HuyaUrlDataModel
    {
        public string Url { get; set; }
        public string Uid { get; set; }
        public long UUid { get; set; }
        public List<HuyaLineModel> Lines { get; set; }
        public List<HuyaBitRateModel> BitRates { get; set; }
    }
    public enum HuyaLineType
    {
        FLV=0,
        HLS=1,
    }
    public class HuyaLineModel
    {
        public string Line { get; set; }
        public string FlvAntiCode { get; set; }
        public string StreamName { get; set; }
        public string HlsAntiCode { get; set; }
        public HuyaLineType LineType { get; set; }
    }
    public class HuyaBitRateModel
    {
        public string Name { get; set; }
        public int BitRate { get; set; }

    }
}
