using AllLive.UWP.Helper;
using AllLive.UWP.Models;
using AllLive.UWP.ViewModels;
using System;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace AllLive.UWP.Views
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class FavoritePage : Page
    {
        readonly FavoriteVM favoriteVM;
        private DateTime mNavigatedFromTime;
        public FavoritePage()
        {
            this.NavigationCacheMode = NavigationCacheMode.Enabled;
            MessageCenter.UpdateFavoriteEvent += MessageCenter_UpdateFavoriteEvent; ;
            this.InitializeComponent();
            favoriteVM = new FavoriteVM();
            mNavigatedFromTime = DateTime.Now;
        }

        private void MessageCenter_UpdateFavoriteEvent(object sender, EventArgs e)
        {
            favoriteVM.Refresh();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (favoriteVM.Items.Count == 0)
            {
                favoriteVM.LoadData();
            }
            else
            {
                TimeSpan timeSinceLoad = DateTime.Now - mNavigatedFromTime;
                if (timeSinceLoad > TimeSpan.FromSeconds(300)) // 300秒为 TTL
                {
                    favoriteVM.LoadData();
                }
            }
            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
            favoriteVM.Items.Clear();
            base.OnNavigatingFrom(e);
            mNavigatedFromTime = DateTime.Now;
        }


        private void ls_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as FavoriteItem;
            var site = MainVM.Sites.FirstOrDefault(x => x.Name == item.SiteName);
            MessageCenter.OpenLiveRoom(site.LiveSite, new Core.Models.LiveRoomItem()
            {
                RoomID = item.RoomID
            });
        }

        private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as MenuFlyoutItem).DataContext as FavoriteItem;
            favoriteVM.RemoveItem(item);
        }

        private void CoreWindow_KeyDown(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.KeyEventArgs args)
        {
            args.Handled = true;
            switch (args.VirtualKey)
            {
                case Windows.System.VirtualKey.G:
                    if (!favoriteVM.Loading && favoriteVM.Items.Count != 0)
                    {
                        favoriteVM.Refresh();
                    }
                    break;
            }
        }
    }
}
