﻿using AllLive.Core.Interface;
using AllLive.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AllLive.Core.Danmaku;
using AllLive.Core.Helper;
using System.Text.RegularExpressions;
using Jint;
using System.Text.Json.Nodes;
/*
* 参考：
* https://github.com/wbt5/real-url/blob/master/douyu.py
*/
namespace AllLive.Core
{
    public class Douyu : ILiveSite
    {
        public string Name => "斗鱼直播";
        public ILiveDanmaku GetDanmaku() => new DouyuDanmaku();
        public async Task<List<LiveCategory>> GetCategores()
        {
            List<LiveCategory> categories = new List<LiveCategory>() {
                new LiveCategory() {
                    ID="PCgame",
                    Name="网游竞技",
                },
                new LiveCategory() {
                    ID="djry",
                    Name="单机热游",
                },
                new LiveCategory() {
                    ID="syxx",
                    Name="手游休闲",
                },
                new LiveCategory() {
                    ID="yl",
                    Name="娱乐天地",
                },
                new LiveCategory() {
                    ID="yz",
                    Name="颜值",
                },
                new LiveCategory() {
                    ID="kjwh",
                    Name="科技文化",
                },
                new LiveCategory() {
                    ID="yp",
                    Name="语言互动",
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
            var result = await HttpUtil.GetString($"https://www.douyu.com/japi/weblist/api/getC2List?shortName={ id}&offset=0&limit=200");
            var obj = JsonNode.Parse(result);
            foreach (var item in obj["data"]["list"].AsArray())
            {
                subs.Add(new LiveSubCategory()
                {
                    Pic = item["squareIconUrlW"].ToString(),
                    ID = item["cid2"].ToString(),
                    ParentID = item["cid1"].ToString(),
                    Name = item["cname2"].ToString(),
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
            var result = await HttpUtil.GetString($"https://www.douyu.com/gapi/rkc/directory/mixList/2_{ category.ID}/{page}");
            var obj = JsonNode.Parse(result);

            foreach (var item in obj["data"]["rl"].AsArray())
            {
                if (item["type"].ToInt32() == 1)
                    categoryResult.Rooms.Add(new LiveRoomItem()
                    {
                        Cover = item["rs16"].ToString(),
                        Online = item["ol"].ToInt32(),
                        RoomID = item["rid"].ToString(),
                        Title = item["rn"].ToString(),
                        UserName = item["nn"].ToString(),
                    });
            }
            categoryResult.HasMore = page < obj["data"]["pgcnt"].ToInt32();
            return categoryResult;
        }
        public async Task<LiveCategoryResult> GetRecommendRooms(int page = 1)
        {
            LiveCategoryResult categoryResult = new LiveCategoryResult()
            {
                Rooms = new List<LiveRoomItem>(),

            };
            var result = await HttpUtil.GetString($"https://www.douyu.com/japi/weblist/apinc/allpage/6/{page}");
            var obj = JsonNode.Parse(result);
            foreach (var item in obj["data"]["rl"].AsArray())
            {
                categoryResult.Rooms.Add(new LiveRoomItem()
                {
                    Cover = item["rs16"].ToString(),
                    Online = item["ol"].ToInt32(),
                    RoomID = item["rid"].ToString(),
                    Title = item["rn"].ToString(),
                    UserName = item["nn"].ToString(),
                });
            }
            categoryResult.HasMore = page < obj["data"]["pgcnt"].ToInt32();
            return categoryResult;
        }
        public async Task<LiveRoomDetail> GetRoomDetail(object roomId)
        {
            var result = await HttpUtil.GetString($"https://www.douyu.com/{roomId}");

            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("user-agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 13_2_3 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/13.0.3 Mobile/15E148 Safari/604.1 Edg/91.0.4472.69");
            var result_json = await HttpUtil.GetString($"https://m.douyu.com/{roomId}", headers);
            var obj = JsonNode.Parse($"{{{result_json.MatchText(@"\$ROOM.=.{(.*?)}")}}}");
            return new LiveRoomDetail()
            {
                Cover = obj["roomSrc"].ToString(),
                Online = ParseHotNum(obj["hn"].ToString()),
                RoomID = obj["rid"].ToString(),
                Title = obj["roomName"].ToString(),
                UserName = obj["nickname"].ToString(),
                UserAvatar = obj["avatar"].ToString(),
                Introduction = "",
                Notice = obj["notice"].ToString(),
                Status = obj["isLive"].ToInt32() == 1,
                DanmakuData = obj["rid"].ToString(),
                Data = await GetPlayArgs(result, obj["rid"].ToString()),
                Url = "https://www.douyu.com/" + roomId
            };
        }

        private async Task<string> GetPlayArgs(string html, string rid)
        {
            //取加密的js
            html = Regex.Match(html, @"(vdwdae325w_64we[\s\S]*function ub98484234[\s\S]*?)function").Groups[1].Value;
            html = Regex.Replace(html, @"eval.*?;}", "strc;}");
            var engine = new Engine();
            var did = "10000000000000000000000000001501";
            var time = Core.Helper.Utils.GetTimestamp();

            engine.Execute(html);
            //调用ub98484234函数，返回格式化后的js
            engine.Execute("ub98484234()");
            var jsCode = engine.GetCompletionValue().ToString();

            var v = Regex.Match(jsCode, @"v=(\d+)").Groups[1].Value;
            //对参数进行MD5，替换掉JS的CryptoJS\.MD5
            var rb = Core.Helper.Utils.ToMD5(rid + did + time + v);

            var jsCode2 = Regex.Replace(jsCode, @"return rt;}\);?", "return rt;}");
            //设置方法名为sign
            jsCode2 = Regex.Replace(jsCode2, @"\(function \(", "function sign(");
            //将JS中的MD5方法直接替换成加密完成的rb
            jsCode2 = Regex.Replace(jsCode2, @"CryptoJS\.MD5\(cb\)\.toString\(\)", $@"""{rb}""");
            engine.Execute(jsCode2);
            //返回参数
            engine.Execute($"sign('{rid}','{did}','{time}')");
            var args = engine.GetCompletionValue().ToString();
            return args;
        }

        public async Task<LiveSearchResult> Search(string keyword, int page = 1)
        {
            LiveSearchResult searchResult = new LiveSearchResult()
            {
                Rooms = new List<LiveRoomItem>(),

            };
            var result = await HttpUtil.GetString($"https://www.douyu.com/japi/search/api/searchShow?kw={ Uri.EscapeDataString(keyword)}&page={ page}&pageSize=20");
            var obj = JsonNode.Parse(result);

            foreach (var item in obj["data"]["relateShow"].AsArray())
            {
                searchResult.Rooms.Add(new LiveRoomItem()
                {
                    Cover = item["roomSrc"].ToString(),
                    Online = ParseHotNum(item["hot"].ToString()),
                    RoomID = item["rid"].ToString(),
                    Title = item["roomName"].ToString(),
                    UserName = item["nickName"].ToString(),
                });
            }
            searchResult.HasMore = obj["data"]["relateShow"].AsArray().Count > 0;
            return searchResult;
        }
        public async Task<List<LivePlayQuality>> GetPlayQuality(LiveRoomDetail roomDetail)
        {
            var data = roomDetail.Data.ToString();
            data += $"&cdn=&rate=0";
            List<LivePlayQuality> qualities = new List<LivePlayQuality>();
            var result = await HttpUtil.PostString($"https://www.douyu.com/lapi/live/getH5Play/{ roomDetail.RoomID}", data);
            var obj = JsonNode.Parse(result);
            var cdns = new List<string>();
            foreach (var item in obj["data"]["cdnsWithName"].AsArray())
            {
                cdns.Add(item["cdn"].ToString());
            }
            foreach (var item in obj["data"]["multirates"].AsArray())
            {
                qualities.Add(new LivePlayQuality()
                {
                    Quality = item["name"].ToString(),
                    Data = new KeyValuePair<int, List<string>>(item["rate"].ToInt32(), cdns),
                });
            }
            return qualities;
        }
        public async Task<List<string>> GetPlayUrls(LiveRoomDetail roomDetail, LivePlayQuality qn)
        {
            var args = roomDetail.Data.ToString();
            var data = (KeyValuePair<int, List<string>>)qn.Data;
            List<string> urls = new List<string>();
            foreach (var item in data.Value)
            {
                var url = await GetUrl(roomDetail.RoomID, args, data.Key, item);
                if (url.Length != 0)
                {
                    urls.Add(url);
                }
            }
            return urls;
        }

        private async Task<string> GetUrl(string rid, string args, int rate, string cdn = "")
        {
            try
            {
                args += $"&cdn={cdn}&rate={rate}";
                var result = await HttpUtil.PostString($"https://www.douyu.com/lapi/live/getH5Play/{rid}", args);
                var obj = JsonNode.Parse(result);
                return obj["data"]["rtmp_url"].ToString() + "/" + System.Net.WebUtility.HtmlDecode(obj["data"]["rtmp_live"].ToString());
            }
            catch (Exception)
            {

                return "";
            }

        }

        private int ParseHotNum(string hn)
        {
            try
            {
                var num = double.Parse(hn.Replace("万", ""));
                if (hn.Contains("万"))
                {
                    num = num * 10000;
                }
                return int.Parse(num.ToString());
            }
            catch (Exception)
            {
                return -999;
            }

        }
    }


}
