using AllLive.Core.Models;
using AllLive.UWP.Helper;
using AllLive.UWP.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace AllLive.UWP.ViewModels
{
    public class FavoriteVM : BaseViewModel
    {
        private readonly List<Task<LiveRoomDetail>> DetailTasks;
        private readonly List<Task<LiveRoomDetail>> DetailTasksShadow;

        public ObservableCollection<FavoriteItem> Items { get; set; }

        public FavoriteVM()
        {
            Items = new ObservableCollection<FavoriteItem>();
            DetailTasks = new List<Task<LiveRoomDetail>>();
            DetailTasksShadow = new List<Task<LiveRoomDetail>>();
            LoadProgress = 0;
        }

        public async void LoadData()
        {
            try
            {
                Loading = true;
                LoadProgress = 0;                

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
                    }
                }
                LoadProgress = 1;
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
            finally
            {
                Loading = false;
                DetailTasks.Clear();
                DetailTasksShadow.Clear();
            }
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
    }
}
