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
        public HistoryVM()
        {
            Items = new ObservableCollection<HistoryItem>();
            CleanCommand = new RelayCommand(Clean);
        }
        public ICommand CleanCommand { get; set; }

        public ObservableCollection<HistoryItem> Items { get; set; }

        public async void LoadData()
        {
            try
            {
                Loading = true;
                var QueryStatusTasks = new List<Task<LiveRoomDetail>>();
                foreach (var item in await DatabaseHelper.GetHistory())
                {
                    Items.Add(item);
                    var site = MainVM.Sites.Find(x => x.Name == item.SiteName);
                    QueryStatusTasks.Add(site.LiveSite.GetRoomDetail(item.RoomID));
                }
                IsEmpty = Items.Count == 0;
                if (!IsEmpty)
                {
                    foreach (var Task in QueryStatusTasks)
                    {
                        var Result = await Task;
                        Items[QueryStatusTasks.IndexOf(Task)].Status = Result.Status;
                    }
                }
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

        public override void Refresh()
        {
            base.Refresh();
            Items.Clear();
            LoadData();
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
