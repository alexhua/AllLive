﻿using AllLive.Core.Danmaku;
using AllLive.Core.Helper;
using AllLive.Core.Interface;
using AllLive.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AllLive.Core
{

    public class BiliBili : ILiveSite
    {
        public string Name => "哔哩哔哩直播";
        public ILiveDanmaku GetDanmaku() => new BiliBiliDanmaku();
        public async Task<List<LiveCategory>> GetCategores()
        {
            List<LiveCategory> categories = new List<LiveCategory>();
            var result = await HttpUtil.GetString("https://api.live.bilibili.com/room/v1/Area/getList?need_entrance=1&parent_id=0");
            var obj = JsonNode.Parse(result);
            foreach (var item in obj["data"].AsArray())
            {
                List<LiveSubCategory> subs = new List<LiveSubCategory>();
                foreach (var subItem in item["list"].AsArray())
                {
                    subs.Add(new LiveSubCategory()
                    {
                        Pic = subItem["pic"].ToString() + "@100w.png",
                        ID = subItem["id"].ToString(),
                        ParentID = subItem["parent_id"].ToString(),
                        Name = subItem["name"].ToString(),
                    });
                }

                categories.Add(new LiveCategory()
                {
                    Children = subs,
                    ID = item["id"].ToString(),
                    Name = item["name"].ToString(),
                });
            }
            return categories;
        }
        public async Task<LiveCategoryResult> GetCategoryRooms(LiveSubCategory category, int page = 1)
        {
            LiveCategoryResult categoryResult = new LiveCategoryResult()
            {
                Rooms = new List<LiveRoomItem>(),

            };
            var result = await HttpUtil.GetString($"https://api.live.bilibili.com/xlive/web-interface/v1/second/getList?platform=web&parent_area_id={category.ParentID}&area_id={category.ID}&sort_type=&page={page}");
            var obj = JsonNode.Parse(result);
            categoryResult.HasMore = obj["data"]["has_more"].ToInt32() == 1;
            foreach (var item in obj["data"]["list"].AsArray())
            {
                categoryResult.Rooms.Add(new LiveRoomItem()
                {
                    Cover = item["cover"].ToString() + "@300w.jpg",
                    Online = item["online"].ToInt32(),
                    RoomID = item["roomid"].ToString(),
                    Title = item["title"].ToString(),
                    UserName = item["uname"].ToString(),
                });
            }
            return categoryResult;
        }
        public async Task<LiveCategoryResult> GetRecommendRooms(int page = 1)
        {
            LiveCategoryResult categoryResult = new LiveCategoryResult()
            {
                Rooms = new List<LiveRoomItem>(),

            };
            var result = await HttpUtil.GetString($"https://api.live.bilibili.com/room/v1/Area/getListByAreaID?areaId=0&sort=online&pageSize=30&page={page}");
            var obj = JsonNode.Parse(result);
            categoryResult.HasMore = (obj["data"].AsArray()).Count > 0;
            foreach (var item in obj["data"].AsArray())
            {
                categoryResult.Rooms.Add(new LiveRoomItem()
                {
                    Cover = item["cover"].ToString() + "@300w.jpg",
                    Online = item["online"].ToInt32(),
                    RoomID = item["roomid"].ToString(),
                    Title = item["title"].ToString(),
                    UserName = item["uname"].ToString(),
                });
            }
            return categoryResult;
        }
        public async Task<LiveRoomDetail> GetRoomDetail(object roomId)
        {
            var result = await HttpUtil.GetString($"https://api.live.bilibili.com/xlive/web-room/v1/index/getH5InfoByRoom?room_id={roomId}");
            var obj = JsonNode.Parse(result);

            return new LiveRoomDetail()
            {
                Cover = obj["data"]["room_info"]["cover"].ToString(),
                Online = obj["data"]["room_info"]["online"].ToInt32(),
                RoomID = obj["data"]["room_info"]["room_id"].ToString(),
                Title = obj["data"]["room_info"]["title"].ToString(),
                UserName = obj["data"]["anchor_info"]["base_info"]["uname"].ToString(),
                Introduction = obj["data"]["room_info"]["description"].ToString(),
                UserAvatar = obj["data"]["anchor_info"]["base_info"]["face"].ToString() + "@100w.jpg",
                Notice = "",
                Status = obj["data"]["room_info"]["live_status"].ToInt32() == 1,
                DanmakuData = obj["data"]["room_info"]["room_id"].ToInt32(),
                Url = "https://live.bilibili.com/" + roomId
            };
        }
        public async Task<LiveSearchResult> Search(string keyword, int page = 1)
        {
            LiveSearchResult searchResult = new LiveSearchResult()
            {
                Rooms = new List<LiveRoomItem>(),

            };
            string Cookie = "buvid3=" + System.Guid.NewGuid(); //"buvid3=948E39F3-DE72-66A8-581F-101ED536193508578infoc";
            var headers = new Dictionary<string, string>
            {
                { "cookie", Cookie }
            };
            var result = await HttpUtil.GetString($"https://api.bilibili.com/x/web-interface/search/type?context=&search_type=live&cover_type=user_cover&page={page}&order=&keyword={Uri.EscapeDataString(keyword)}&category_id=&__refresh__=true&_extra=&highlight=0&single_column=0", headers);
            var liveRooms = JsonNode.Parse(result)["data"]["result"]["live_room"] ?? new JsonArray();
            foreach (var item in liveRooms.AsArray())
            {
                searchResult.Rooms.Add(new LiveRoomItem()
                {
                    Cover = "https:" + item["cover"].ToString() + "@300w.jpg",
                    Online = item["online"].ToInt32(),
                    RoomID = item["roomid"].ToString(),
                    Title = Regex.Replace(item["title"].ToString(), @"<em.*?/em>", ""),
                    UserName = item["uname"].ToString(),
                });
            }
            searchResult.HasMore = searchResult.Rooms.Count > 0;
            return searchResult;
        }

        private string Guid()
        {
            throw new NotImplementedException();
        }

        public async Task<List<LivePlayQuality>> GetPlayQuality(LiveRoomDetail roomDetail)
        {
            List<LivePlayQuality> qualities = new List<LivePlayQuality>();
            var result = await HttpUtil.GetString($"https://api.live.bilibili.com/room/v1/Room/playUrl?cid={roomDetail.RoomID}&qn=&platform=web");
            var obj = JsonNode.Parse(result);
            var msg = obj["message"].ToString();
            if (!msg.Equals("0")) return await GetPlayQualityV2(roomDetail);
            foreach (var item in obj["data"]["quality_description"].AsArray())
            {
                qualities.Add(new LivePlayQuality()
                {
                    Quality = item["desc"].ToString(),
                    Data = item["qn"].ToInt32(),
                });
            }
            return qualities;
        }
        public async Task<List<LivePlayQuality>> GetPlayQualityV2(LiveRoomDetail roomDetail)
        {
            List<LivePlayQuality> qualities = new List<LivePlayQuality>();
            var result = await HttpUtil.GetString($"https://api.live.bilibili.com/xlive/web-room/v2/index/getRoomPlayInfo?room_id={roomDetail.RoomID}&protocol=0,1&format=0,1,2&codec=0,1,2&qn=0&platform=web");
            var obj = JsonNode.Parse(result);
            var playUrlInfo = obj["data"]["playurl_info"]["playurl"];
            var qnDescList = playUrlInfo["g_qn_desc"].AsArray();
            foreach (var protocol in playUrlInfo["stream"].AsArray())
            {
                foreach (var format in protocol["format"].AsArray())
                {
                    foreach (var codec in format["codec"].AsArray().Reverse())
                    {
                        var qn = codec["current_qn"].ToInt32();
                        qualities.Add(new LivePlayQuality()
                        {
                            Quality = $"{format["format_name"]}-{codec["codec_name"]}-{getQnName(qn, qnDescList)}".ToUpper(),
                            Data = qn,
                        });
                    }
                }
            }
            return qualities;
        }
        private string getQnName(int qn, JsonArray qnDescList)
        {
            foreach (var qnItem in qnDescList)
            {
                if (qnItem["qn"].ToInt32() == qn)
                {
                    return qnItem["desc"].ToString();
                }
            }
            return "默认";
        }

        public async Task<List<string>> GetPlayUrls(LiveRoomDetail roomDetail, LivePlayQuality qn)
        {
            List<string> urls = new List<string>();
            var result = await HttpUtil.GetString($"https://api.live.bilibili.com/room/v1/Room/playUrl?cid={roomDetail.RoomID}&qn={qn.Data}&platform=web");
            var obj = JsonNode.Parse(result);
            var msg = obj["message"].ToString();
            if (!msg.Equals("0")) return await GetPlayUrlsV2(roomDetail, qn);
            foreach (var item in obj["data"]["durl"].AsArray())
            {
                urls.Add(item["url"].ToString());
            }
            return urls;
        }
        public async Task<List<string>> GetPlayUrlsV2(LiveRoomDetail roomDetail, LivePlayQuality qn)
        {
            List<string> urls = new List<string>();
            var result = await HttpUtil.GetString($"https://api.live.bilibili.com/xlive/web-room/v2/index/getRoomPlayInfo?room_id={roomDetail.RoomID}&protocol=0,1&format=0,1,2&codec=0,1,2&qn={qn.Data}&platform=web");
            var obj = JsonNode.Parse(result);
            var playUrlInfo = obj["data"]["playurl_info"]["playurl"];
            var qnInfo = qn.Quality.ToLower().Split('-');
            string lineFormat = qnInfo[0], lineCodec = qnInfo[1];

            foreach (var protocol in playUrlInfo["stream"].AsArray())
            {
                foreach (var format in protocol["format"].AsArray())
                {
                    if (!lineFormat.Equals(format["format_name"].ToString())) continue;
                    foreach (var codec in format["codec"].AsArray())
                    {
                        if (!lineCodec.Equals(codec["codec_name"].ToString())) continue;
                        foreach (var urlInfo in codec["url_info"].AsArray())
                        {
                            urls.Add(urlInfo["host"].ToString() + codec["base_url"] + urlInfo["extra"]);
                        }
                    }
                }
            }
            return urls;
        }
    }
}
