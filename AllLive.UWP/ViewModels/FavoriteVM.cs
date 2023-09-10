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
        public ObservableCollection<FavoriteItem> Items { get; set; }

        public FavoriteVM()
        {
            Items = new ObservableCollection<FavoriteItem>();
        }

        public async void LoadData()
        {
            try
            {
                Loading = true;
                var DetailTasks = new List<Task<LiveRoomDetail>>();
                foreach (var item in await DatabaseHelper.GetFavorites())
                {
                    Items.Add(item);
                    var Site = MainVM.Sites.Find(x => x.Name == item.SiteName);
                    DetailTasks.Add(Site.LiveSite.GetRoomDetail(item.RoomID));
                }
                IsEmpty = Items.Count == 0;

                if (!IsEmpty)
                {
                    for (var i = 0; i < Items.Count; i++)
                    {
                        var item = Items[i];
                        try
                        {
                            var Result = await DetailTasks[i];
                            item.Status = Result != null && Result.Status;

                            if (item.Status && (!item.UserName.Equals(Result.UserName) || !item.Photo.Equals(Result.UserAvatar)))
                            {
                                item.UserName = Result.UserName;
                                item.Photo = Result.UserAvatar;
                                DatabaseHelper.UpdateFavorite(item);
                            }
                        }
                        catch
                        {
                            Utils.ShowMessageToast($"{item.UserName}的房间: {item.RoomID}，获取信息异常。");
                        }
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
