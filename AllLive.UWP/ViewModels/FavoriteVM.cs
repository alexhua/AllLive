using AllLive.Core.Models;
using AllLive.UWP.Helper;
using AllLive.UWP.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Windows.Storage.Pickers;
using Windows.Storage;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.UI.Popups;
using System.Threading.Tasks;
using System.Linq;

namespace AllLive.UWP.ViewModels
{
    public class FavoriteVM : BaseViewModel
    {
        private readonly List<Task<LiveRoomDetail>> DetailTasks;
        private readonly List<Task<LiveRoomDetail>> DetailTasksShadow;

        public FavoriteVM()
        {
            Items = new ObservableCollection<FavoriteItem>();
            InputCommand = new RelayCommand(Input);
            OutputCommand = new RelayCommand(Output);
            TipCommand = new RelayCommand(Tip);
            DetailTasks = new List<Task<LiveRoomDetail>>();
            DetailTasksShadow = new List<Task<LiveRoomDetail>>();
        }

        public ICommand InputCommand { get; set; }
        public ICommand OutputCommand { get; set; }
        public ICommand TipCommand { get; set; }

        private ObservableCollection<FavoriteItem> _items;
        public ObservableCollection<FavoriteItem> Items
        {
            get { return _items; }
            set { _items = value; DoPropertyChanged("Items"); }
        }


        private bool _loadingLiveStatus;

        public bool LoaddingLiveStatus
        {
            get { return _loadingLiveStatus; }
            set { _loadingLiveStatus = value; DoPropertyChanged("LoaddingLiveStatus"); }
        }

        public async void LoadData()
        {
            try
            {
                Loading = true;
                DetailTasks.Clear();
                DetailTasksShadow.Clear();

                foreach (var item in await DatabaseHelper.GetFavorites())
                {
                    item.Title = item.SiteName;
                    Items.Add(item);
                    var Site = MainVM.Sites.Find(x => x.Name == item.SiteName);
                    var task = Site.LiveSite.GetRoomDetail(item.RoomID);
                    DetailTasks.Add(task);
                    DetailTasksShadow.Add(task);
                }
                IsEmpty = Items.Count == 0;
                LoadLiveStatus();
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
            finally
            {
                Loading = false;
            }
        }

        public async void LoadLiveStatus()
        {
            LoaddingLiveStatus = true;
            LoadProgress = 0;
            while (DetailTasks.Count > 0)
            {
                var finishedTask = await Task.WhenAny(DetailTasks);
                var i = DetailTasksShadow.IndexOf(finishedTask);
                var item = Items[i];
                try
                {
                    var result = await finishedTask;
                    if (result.Status)
                    {
                        item.Status = result.Status;
                        item.Cover = result.Cover;
                        if (!string.IsNullOrEmpty(result.Title))
                        {
                            item.Title += $" - {result.Title}";
                        }
                        if (!item.UserName.Equals(result.UserName) || !item.Photo.Equals(result.UserAvatar))
                        {
                            item.UserName = result.UserName;
                            item.Photo = result.UserAvatar;
                            DatabaseHelper.UpdateFavorite(item);
                        }
                    }
                }
                catch
                {
                    Utils.ShowMessageToast($"{item.UserName}的房间: {item.RoomID}，获取信息异常。");
                }
                finally
                {
                    DetailTasks.Remove(finishedTask);
                    LoadProgress += 1d / Items.Count;
                    // 排序，直播的在前面
                    //Items = new ObservableCollection<FavoriteItem>(Items.OrderByDescending(x => x.Status));
                }
            }
            LoadProgress = 1;
            LoaddingLiveStatus = false;
        }

        public override void Refresh()
        {
            base.Refresh();
            Items.Clear();
            LoadData();
        }

        public void RemoveItem(FavoriteItem item)
        {
            try
            {
                DatabaseHelper.DeleteFavorite(item.ID);
                Items.Remove(item);
                IsEmpty = Items.Count == 0;
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }

        }

        public async void Input()
        {

            // 打开文件选择器
            FileOpenPicker picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".json");
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.ViewMode = PickerViewMode.List;
            picker.CommitButtonText = "导入";

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    var json = await FileIO.ReadTextAsync(file);
                    var items = JsonSerializer.Deserialize<List<FavoriteJsonItem>>(json);
                    foreach (var item in items)
                    {

                        DatabaseHelper.AddFavorite(new FavoriteItem()
                        {
                            SiteName = item.SiteName,
                            RoomID = item.RoomId,
                            UserName = item.UserName,
                            Photo = item.Face,
                        });
                    }
                    Utils.ShowMessageToast("导入成功");
                    Refresh();
                }
                catch (Exception ex)
                {
                    HandleError(ex);
                    Utils.ShowMessageToast("导入失败");
                }
            }
        }

        public async void Output()
        {
            // 打开文件选择器
            FileSavePicker picker = new FileSavePicker();
            picker.FileTypeChoices.Add("Json", new List<string>() { ".json" });
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.SuggestedFileName = "favorite.json";

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                try
                {
                    var items = new List<FavoriteJsonItem>();
                    foreach (var item in Items)
                    {
                        var siteId = "";
                        switch (item.SiteName)
                        {
                            case "哔哩哔哩直播":
                                siteId = "bilibili";
                                break;
                            case "斗鱼直播":
                                siteId = "douyu";
                                break;
                            case "虎牙直播":
                                siteId = "huya";
                                break;
                            case "抖音直播":
                                siteId = "douyin";
                                break;
                        }

                        items.Add(new FavoriteJsonItem()
                        {
                            SiteId = siteId,
                            Id = $"{siteId}_{item.RoomID}",
                            RoomId = item.RoomID,
                            UserName = item.UserName,
                            Face = item.Photo,
                            AddTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.M")
                        });
                    }
                    var json = JsonSerializer.Serialize(items);
                    await FileIO.WriteTextAsync(file, json);
                    Utils.ShowMessageToast("导出成功");
                }
                catch (Exception ex)
                {
                    HandleError(ex);
                    Utils.ShowMessageToast("导出失败");
                }
            }


        }

        public void Tip()
        {
            MessageDialog dialog = new MessageDialog(@"该程序兼容Simple Live，您可以导入Simple Live的关注数据，导出的数据也可以在Simple Live中导入。", "导入导出说明");
            _ = dialog.ShowAsync();
        }
    }

    public class FavoriteJsonItem
    {
        [JsonPropertyName("siteId")]
        public string SiteId;

        [JsonPropertyName("id")]
        public string Id;

        [JsonPropertyName("roomId")]
        public string RoomId;

        [JsonPropertyName("userName")]
        public string UserName;

        [JsonPropertyName("face")]
        public string Face;

        [JsonPropertyName("addTime")]
        public string AddTime;

        [JsonIgnore]
        public string SiteName
        {
            get
            {
                switch (SiteId)
                {
                    case "bilibili":
                        return "哔哩哔哩直播";
                    case "douyu":
                        return "斗鱼直播";
                    case "huya":
                        return "虎牙直播";
                    case "douyin":
                        return "抖音直播";
                    default:
                        return "未知";
                }
            }
        }

    }
}
