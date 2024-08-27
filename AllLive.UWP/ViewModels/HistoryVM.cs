﻿using AllLive.Core.Models;
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

        private bool _loadingLiveStatus;

        public bool LoadingLiveStatus
        {
            get { return _loadingLiveStatus; }
            set { _loadingLiveStatus = value; DoPropertyChanged("LoadingLiveStatus"); }
        }

        public async void LoadData()
        {
            Loading = true;
            LoadingLiveStatus = true;
            LoadingProgress = 0;
            var detailTasks = new List<Task<HistoryItem>>();
            try
            {
                await foreach (var item in DatabaseHelper.GetHistory())
                {
                    Items.Add(item);
                    detailTasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var Site = MainVM.Sites.Find(x => x.Name == item.SiteName);
                            item.Title = Site.Name;
                            var detail = await Site.LiveSite.GetRoomDetail(item.RoomID);
                            item.Status = detail.Status;
                            if (!string.IsNullOrEmpty(detail.Title))
                            {
                                item.Title += $" - {detail.Title}";
                            }
                        }
                        catch
                        {
                            return item;
                        }
                        return null;
                    }));
                }
                while (detailTasks.Count > 0)
                {
                    var task = await Task.WhenAny(detailTasks);
                    var item = await task;
                    if (item != null)
                    {
                        Utils.ShowMessageToast($"{item.UserName}的房间: {item.RoomID}，获取信息异常。");
                    }
                    detailTasks.Remove(task);
                    LoadingProgress += 1d / Items.Count;
                }
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
            finally
            {
                IsEmpty = Items.Count == 0;
                LoadingProgress = 1;
                Loading = false;
                LoadingLiveStatus = false;
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
