using AllLive.Core.Models;
using AllLive.UWP.Helper;
using AllLive.UWP.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AllLive.UWP.ViewModels
{
    public class HistoryVM : BaseViewModel
    {
        readonly List<Task<LiveRoomDetail>> DetailTasks;
        public HistoryVM()
        {
            Items = new ObservableCollection<HistoryItem>();
            DetailTasks = new List<Task<LiveRoomDetail>>();
            CleanCommand = new RelayCommand(Clean);
            LoadProgress = 0;
        }
        public ICommand CleanCommand { get; set; }

        public ObservableCollection<HistoryItem> Items { get; set; }

        private bool _loadingLiveStatus;

        public bool LoaddingLiveStatus
        {
            get { return _loadingLiveStatus; }
            set { _loadingLiveStatus = value; DoPropertyChanged("LoaddingLiveStatus"); }
        }

        public async void LoadData()
        {
            Loading = true;
            DetailTasks.Clear();
            try
            {
                foreach (var item in await DatabaseHelper.GetHistory())
                {
                    Items.Add(item);
                    var Site = MainVM.Sites.Find(x => x.Name == item.SiteName);
                    var detailTask = Site.LiveSite.GetRoomDetail(item.RoomID);
                    DetailTasks.Add(detailTask);
                }
                IsEmpty = Items.Count == 0;
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
            LoadProgress = 0;
            LoaddingLiveStatus = true;
            for (var i = 0; i < DetailTasks.Count; i++)
            {
                try
                {
                    var roomDetail = await DetailTasks[i];
                    if (roomDetail != null && roomDetail.Status)
                    {
                        Items[i].Status = roomDetail.Status;
                    }
                }
                catch
                {
                    Utils.ShowMessageToast($"{Items[i].UserName}的房间: {Items[i].RoomID}，获取信息异常。");
                }
                finally
                {
                    LoadProgress += 1d / DetailTasks.Count;
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
            LoadLiveStatus();
        }

        public void RemoveItem(HistoryItem item)
        {
            try
            {
                DatabaseHelper.DeleteHistory(item.ID);
                Items.Remove(item);
                IsEmpty = Items.Count == 0;
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }

        }
        public async void Clean()
        {
            try
            {

                var result = await Utils.ShowDialog("清空记录", $"确定要清除全部观看记录吗?");
                if (!result)
                {
                    return;
                }

                DatabaseHelper.DeleteHistory();
                Items.Clear();
                IsEmpty = true;
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }
    }
}
