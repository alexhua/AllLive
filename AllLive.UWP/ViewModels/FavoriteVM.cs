using AllLive.UWP.Helper;
using AllLive.UWP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

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
                foreach (var item in await DatabaseHelper.GetFavorites())
                { 
                    Items.Add(item);
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

        public async void LoadStatus()
        {
            try
            {
                Loading = true;
                foreach (var item in Items)
                {
                    var site = MainVM.Sites.Find(x => x.Name == item.SiteName);
                    var result = await site?.LiveSite.GetRoomDetail(item.RoomID);
                    item.Status = result.Status;
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
            LoadStatus();
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
