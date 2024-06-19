﻿using AllLive.Core.Interface;
using AllLive.Core.Helper;
using AllLive.UWP.Helper;
using AllLive.UWP.Models;
using AllLive.UWP.ViewModels;
using AllLive.UWP.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using AllLive.Core.Models;
using Windows.ApplicationModel.Core;
using System.Threading.Tasks;

// https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x804 上介绍了“空白页”项模板

namespace AllLive.UWP
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {

        public MainPage()
        {
           
            this.NavigationCacheMode = NavigationCacheMode.Enabled;
            this.InitializeComponent();
            MessageCenter.UpdatePanelDisplayModeEvent += MessageCenter_UpdatePanelDisplayModeEvent;
            SetPaneMode();
        }

        private void MessageCenter_UpdatePanelDisplayModeEvent(object sender, EventArgs e)
        {
            SetPaneMode();
        }

        private void SetPaneMode()
        {
            if (SettingHelper.GetValue<int>(SettingHelper.PANE_DISPLAY_MODE, 0) == 0)
            {
                navigationView.PaneDisplayMode = Microsoft.UI.Xaml.Controls.NavigationViewPaneDisplayMode.Left;
            }
            else
            {
                navigationView.PaneDisplayMode = Microsoft.UI.Xaml.Controls.NavigationViewPaneDisplayMode.Top;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _ = BiliAccount.Instance.InitLoginInfo();
            _ = Helper.Utils.CheckVersion();

        }

        private void NavigationView_SelectionChanged(Microsoft.UI.Xaml.Controls.NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            var item = args.SelectedItem as Microsoft.UI.Xaml.Controls.NavigationViewItem;
            if (item.Tag.ToString() == "设置" || item.Tag.ToString() == "Settings")
            {
                item.Tag = "SettingsPage";
            }
            frame.Navigate(Type.GetType("AllLive.UWP.Views." + item.Tag));

        }

        private async void searchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (string.IsNullOrEmpty(args.QueryText))
            {
                Helper.Utils.ShowMessageToast("关键字不能为空");
                return;
            }
            if (!await ParseUrl(args.QueryText))
            {
                this.Frame.Navigate(typeof(SearchPage), args.QueryText);
            }
        }

        private async Task<bool> ParseUrl(string url)
        {
            var parseResult = await SiteParser.ParseUrl(url);
            if (parseResult.Item1 != LiveSite.Unknown && !string.IsNullOrEmpty(parseResult.Item2))
            {
                this.Frame.Navigate(typeof(LiveRoomPage), new PageArgs()
                {
                    Site = MainVM.Sites[(int)parseResult.Item1].LiveSite,
                    Data = new LiveRoomItem()
                    {
                        RoomID = parseResult.Item2,
                    }
                });
                return true;
            }
            else
            {
                return false;
            }


        }

        private void navigationView_Loaded(object sender, RoutedEventArgs e)
        {
            navigationView.IsPaneOpen = false;
        }
    }
}
