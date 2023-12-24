using AllLive.Core.Interface;
using AllLive.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AllLive.Core.Danmaku;
using AllLive.Core.Helper;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;
using System.Security.Cryptography;
using System.Resources;
using System.Text.Json.Nodes;

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
            var obj = JsonNode.Parse(result);
            foreach (var item in obj["data"].AsArray())
            {
                subs.Add(new LiveSubCategory()
                {
                    Pic = $"https://huyaimg.msstatic.com/cdnimage/game/{item["gid"]}-MS.jpg",
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
            var obj = JsonNode.Parse(result);
            foreach (var item in obj["data"]["datas"].AsArray())
            {
                var cover = item["screenshot"].ToString();
                if (!cover.Contains("?x-oss-process"))
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
            var obj = JsonNode.Parse(result);

            foreach (var item in obj["data"]["datas"].AsArray())
            {
                var cover = item["screenshot"].ToString();
                if (!cover.Contains("?x-oss-process"))
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
            headers.Add("user-agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 15_2_3 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/13.0.3 Mobile/15E148 Safari/604.1");
            var result = await HttpUtil.GetString($"https://m.huya.com/{roomId}", headers);
            var jsonStr = Regex.Match(result, @"window\.HNF_GLOBAL_INIT.=.\{(.*?)\}.</script>", RegexOptions.Singleline).Groups[1].Value;
            var jsonObj = JsonNode.Parse($"{{{jsonStr}}}");
            var title = jsonObj["roomInfo"]["tLiveInfo"]["sRoomName"].ToString();
            if (string.IsNullOrEmpty(title))
            {
                title = jsonObj["roomInfo"]["tLiveInfo"]["sIntroduction"].ToString();
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
                Data = jsonObj["roomInfo"]["tLiveInfo"]["tLiveStreamInfo"],
                DanmakuData = new HuyaDanmakuArgs(
                    jsonObj["roomInfo"]["tLiveInfo"]["lYyid"].ToInt64(),
                    result.MatchText(@"lChannelId"":([0-9]+)").ToInt64(),
                    result.MatchText(@"lSubChannelId"":([0-9]+)").ToInt64()
                ),
                Url = "https://www.huya.com/" + roomId
            };
        }
        public async Task<LiveSearchResult> Search(string keyword, int page = 1)
        {
            LiveSearchResult searchResult = new LiveSearchResult()
            {
                Rooms = new List<LiveRoomItem>(),

            };
            var result = await HttpUtil.GetUtf8String($"https://search.cdn.huya.com/?m=Search&do=getSearchContent&q={Uri.EscapeDataString(keyword)}&uid=0&v=4&typ=-5&livestate=0&rows=20&start={(page - 1) * 20}");
            var obj = JsonNode.Parse(result);

            foreach (var item in obj["response"]["3"]["docs"].AsArray())
            {
                var cover = item["game_screenshot"].ToString();
                if (!cover.Contains("?x-oss-process"))
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

        private string GenerateWsSecret(string StreamName)
        {
            const string fm = "DWq8BcJ3h6DJt6TY";
            const string ctype = "tars_mobile";
            const string t = "103";
            long timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            string wsTime = ((long)Math.Truncate(timestamp / 1e3)).ToString("x");
            long uid = (long)(timestamp % 1e10 * 1e5 + Tup.Utility.Util.Random() % 1e5) % 4294967295;
            long uuid = (long)(timestamp % 1e10 * 1e3 + Tup.Utility.Util.Random() % 1e3) % 4294967295;
            long seqid = uid + timestamp;
            string s = Utils.ToMD5($"{seqid}|{ctype}|{t}");
            string wsSecret = Utils.ToMD5($"{fm}_{uid}_{StreamName}_{s}_{wsTime}");
            return $"wsSecret={wsSecret}&wsTime={wsTime}&seqid={seqid}&uid={uid}&uuid={uuid}";
        }
        private string PickupUrlParams(params string[] antiCodes)
        {
            string result = "ver=1";
            for (int i = 3; i < antiCodes.Length; i++)
            {
                result += '&' + antiCodes[i];
            }
            return result;
        }
        public Task<List<LivePlayQuality>> GetPlayQuality(LiveRoomDetail roomDetail)
        {
            JsonNode liveStreamInfo = roomDetail.Data as JsonNode;
            var streamRatio = liveStreamInfo["mStreamRatio"]["value"];
            List<LivePlayQuality> qualities = new List<LivePlayQuality>();
            foreach (JsonNode bitRateInfo in liveStreamInfo["vBitRateInfo"]["value"].AsArray())
            {   /* Flv */
                var quality = new LivePlayQuality()
                {
                    Quality = bitRateInfo["sDisplayName"].ToString(),
                    Data = new List<string>(),
                    LineNames = new List<string>()
                };
                foreach (JsonNode streamInfo in liveStreamInfo["vStreamInfo"]["value"].AsArray())
                {
                    var cdnType = streamInfo["sCdnType"].ToString();
                    if (streamRatio[cdnType].ToInt32() < 0) continue; //可能为无效线路
                    var lineName = streamInfo["iLineIndex"].ToInt32() == 0 ? "" : $"线路{streamInfo["iLineIndex"].ToInt32()}";
                    quality.LineNames.Add(lineName);
                    ((List<string>)quality.Data).Add(
                        $"{streamInfo["sFlvUrl"]}/{streamInfo["sStreamName"]}.{streamInfo["sFlvUrlSuffix"]}?" +
                        $"{GenerateWsSecret(streamInfo["sStreamName"].ToString())}&ratio={bitRateInfo["iBitRate"]}&" +
                        PickupUrlParams(streamInfo["sFlvAntiCode"].ToString().Split('&'))
                    );
                }
                qualities.Add(quality);
                ///* Hls */
                //var qualityHls = new LivePlayQuality()
                //{
                //    Quality = bitRateInfo["sDisplayName"].ToString() + "-HLS",
                //    Data = new List<string>()
                //};
                //foreach (JsonNode streamInfo in liveStreamInfo["vStreamInfo"]["value"].AsArray())
                //{
                //    ((List<string>)qualityHls.Data).Add(
                //        $"{streamInfo["sHlsUrl"]}/{streamInfo["sStreamName"]}.{streamInfo["sHlsUrlSuffix"]}?" +
                //        $"{generateWsSecret(streamInfo["sStreamName"].ToString())}&ratio={bitRateInfo["iBitRate"]}&" +
                //        pickupUrlParams(streamInfo["sHlsAntiCode"].ToString().Split('&'))
                //    );
                //}
                //qualities.Add(qualityHls);
                if (qualities.Count == 4) break;
            }
            return Task.FromResult(qualities);
        }

        public Task<List<string>> GetPlayUrls(LiveRoomDetail roomDetail, LivePlayQuality qn)
        {
            return Task.FromResult(qn.Data as List<string>);
        }



    }
}
