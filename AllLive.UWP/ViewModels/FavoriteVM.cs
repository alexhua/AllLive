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
                var QueryStatusTasks = new List<Task<LiveRoomDetail>>();
                foreach (var item in await DatabaseHelper.GetFavorites())
                {
                    Items.Add(item);
                    var Site = MainVM.Sites.Find(x => x.Name == item.SiteName);
                    QueryStatusTasks.Add(Site.LiveSite.GetRoomDetail(item.RoomID));
                }
                IsEmpty = Items.Count == 0;

                if (!IsEmpty)
                {
                    var Details = await Task.WhenAll<LiveRoomDetail>(QueryStatusTasks);
                    foreach (var Item in Items)
                    {
                        Item.Status = Details[Items.IndexOf(Item)].Status;
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
