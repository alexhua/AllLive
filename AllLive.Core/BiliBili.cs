﻿using AllLive.Core.Interface;
using AllLive.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AllLive.Core.Danmaku;
using AllLive.Core.Helper;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using WebSocketSharp;
using System.Linq;

namespace AllLive.Core
{

    public class BiliBili : ILiveSite
    {
        public string Name => "哔哩哔哩直播";
        public ILiveDanmaku GetDanmaku() => new BiliBiliDanmaku();
        /// <summary>
        /// 哔哩哔哩Cookie
        /// </summary>
        public string Cookie { get; set; }
        /// <summary>
        /// 哔哩哔哩用户ID
        /// </summary>
        public long UserId { get; set; }

        private Dictionary<string, string> GetRequestHeader(bool withBuvid3 = false)
        {
            if (string.IsNullOrEmpty(Cookie))
            {
                var headers = new Dictionary<string, string>();
                if (withBuvid3)
                {
                    headers.Add("cookie", "buvid3=infoc;");
                }
                return headers;
            }
            else
            {
                return new Dictionary<string, string>() {
                    { "cookie",Cookie },
                };
            }
        }

        public async Task<List<LiveCategory>> GetCategores()
        {
            List<LiveCategory> categories = new List<LiveCategory>();
            var result = await HttpUtil.GetString("https://api.live.bilibili.com/room/v1/Area/getList?need_entrance=1&parent_id=0", headers: GetRequestHeader());
            var obj = JObject.Parse(result);
            foreach (var item in obj["data"])
            {
                List<LiveSubCategory> subs = new List<LiveSubCategory>();
                foreach (var subItem in item["list"])
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
            var result = await HttpUtil.GetString($"https://api.live.bilibili.com/xlive/web-interface/v1/second/getList?platform=web&parent_area_id={category.ParentID}&area_id={category.ID}&sort_type=&page={page}", headers: GetRequestHeader());
            var obj = JObject.Parse(result);
            categoryResult.HasMore = obj["data"]["has_more"].ToInt32() == 1;
            foreach (var item in obj["data"]["list"])
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
            var result = await HttpUtil.GetString($"https://api.live.bilibili.com/room/v1/Area/getListByAreaID?areaId=0&sort=online&pageSize=30&page={page}", headers: GetRequestHeader());
            var obj = JObject.Parse(result);
            categoryResult.HasMore = ((JArray)obj["data"]).Count > 0;
            foreach (var item in obj["data"])
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
            var result = await HttpUtil.GetString($"https://api.live.bilibili.com/xlive/web-room/v1/index/getH5InfoByRoom?room_id={roomId}", headers: GetRequestHeader());
            var obj = JObject.Parse(result);

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
            var result = await HttpUtil.GetString($"https://api.bilibili.com/x/web-interface/search/type?context=&search_type=live&cover_type=user_cover&page={page}&order=&keyword={Uri.EscapeDataString(keyword)}&category_id=&__refresh__=true&_extra=&highlight=0&single_column=0", headers: GetRequestHeader(true));
            var obj = JObject.Parse(result);

            foreach (var item in obj["data"]["result"]["live_room"])
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
        public async Task<List<LivePlayQuality>> GetPlayQuality(LiveRoomDetail roomDetail)
        {
            if (string.IsNullOrEmpty(Cookie))
            {
                return await GetPlayQualityOld(roomDetail.RoomID);
            }
            else
            {
                return await GetPlayQualityNew(roomDetail.RoomID);
            }
        }
        /// <summary>
        /// 新的获取清晰度方式，需要登录
        /// </summary>
        /// <param name="roomID"></param>
        /// <returns></returns>
        private async Task<List<LivePlayQuality>> GetPlayQualityNew(string roomID)
        {

            List<LivePlayQuality> qualities = new List<LivePlayQuality>();
            var result = await HttpUtil.GetString($"https://api.live.bilibili.com/xlive/web-room/v2/index/getRoomPlayInfo",
                headers: GetRequestHeader(),
                queryParameters: new Dictionary<string, string>() {
                    { "room_id", roomID } ,
                    { "protocol", "0,1" },
                    { "format", "0,1,2"},
                    { "codec","0,1"},
                    { "platform", "web"}
                }
            );
            var obj = JObject.Parse(result);
            var qualitiesMap = new Dictionary<int, string>();
            foreach (var item in obj["data"]["playurl_info"]["playurl"]["g_qn_desc"])
            {
                qualitiesMap[item["qn"].ToObject<int>()] =
                    item["desc"].ToString();
            }
            foreach (var item in obj["data"]["playurl_info"]["playurl"]["stream"][0]["format"][0]["codec"][0]["accept_qn"])
            {
                var qualityItem = new LivePlayQuality()
                {
                    Quality = qualitiesMap[item.ToObject<int>()] ?? "未知清晰度",
                    Data = item,
                };
                qualities.Add(qualityItem);
            }
            return qualities;
        }

        /// <summary>
        /// 旧的获取清晰度方式，部分直播看不了
        /// </summary>
        /// <param name="roomID"></param>
        /// <returns></returns>
        private async Task<List<LivePlayQuality>> GetPlayQualityOld(string roomID)
        {

            List<LivePlayQuality> qualities = new List<LivePlayQuality>();
            var result = await HttpUtil.GetString($"https://api.live.bilibili.com/room/v1/Room/playUrl?cid={roomID}&qn=&platform=web", headers: GetRequestHeader());
            var obj = JObject.Parse(result);
            foreach (var item in obj["data"]["quality_description"])
            {
                qualities.Add(new LivePlayQuality()
                {
                    Quality = item["desc"].ToString(),
                    Data = item["qn"].ToInt32(),
                });
            }
            return qualities;
        }

        public async Task<List<string>> GetPlayUrls(LiveRoomDetail roomDetail, LivePlayQuality qn)
        {
            if (string.IsNullOrEmpty(Cookie))
            {
                return await GetPlayUrlsOld(roomDetail.RoomID, qn.Data);
            }
            else
            {
                return await GetPlayUrlsNew(roomDetail.RoomID, qn.Data);
            }
        }
        private async Task<List<string>> GetPlayUrlsNew(string roomID, object qn)
        {
            List<string> urls = new List<string>();
            List<LivePlayQuality> qualities = new List<LivePlayQuality>();
            var result = await HttpUtil.GetString($"https://api.live.bilibili.com/xlive/web-room/v2/index/getRoomPlayInfo",
                headers: GetRequestHeader(),
                queryParameters: new Dictionary<string, string>() {
                    { "room_id", roomID } ,
                    { "protocol", "0,1" },
                    { "format", "0,2"},
                    { "codec","0"},
                    { "platform", "web" },
                    { "qn",qn.ToString()}
                }
            );
            var obj = JObject.Parse(result);
            var streamList = obj["data"]["playurl_info"]["playurl"]["stream"];
            foreach (var streamItem in streamList)
            {
                var formatList = streamItem["format"];
                foreach (var formatItem in formatList)
                {
                    var codecList = formatItem["codec"];
                    foreach (var codecItem in codecList)
                    {
                        var urlList = codecItem["url_info"];
                        var baseUrl = codecItem["base_url"].ToString();
                        foreach (var urlItem in urlList)
                        {
                            urls.Add(
                              $"{urlItem["host"].ToString()}{baseUrl.ToString()}{urlItem["extra"].ToString()}"
                            );
                        }
                    }
                }
            }

            // 对链接进行排序，包含mcdn的在后
            urls = urls.OrderBy(x => x.Contains("mcdn")).ToList();

            return urls;
        }
        /// <summary>
        /// 旧的获取播放地址方式，部分直播看不了
        /// </summary>
        /// <param name="roomID"></param>
        /// <param name="qn"></param>
        /// <returns></returns>
        private async Task<List<string>> GetPlayUrlsOld(string roomID, object qn)
        {
            List<string> urls = new List<string>();
            var result = await HttpUtil.GetString($"https://api.live.bilibili.com/room/v1/Room/playUrl?cid={roomID}&qn={qn}&platform=web", headers: GetRequestHeader());
            var obj = JObject.Parse(result);
            foreach (var item in obj["data"]["durl"])
            {
                urls.Add(item["url"].ToString());
            }
            return urls;
        }

        public async Task<bool> GetLiveStatus(object roomId)
        {
            var resp = await HttpUtil.GetString($"https://api.live.bilibili.com/room/v1/Room/get_info?room_id={roomId}", headers: GetRequestHeader());
            var obj = JObject.Parse(resp);
            return obj["data"]["live_status"].ToObject<int>() == 1;
        }
    }
}
