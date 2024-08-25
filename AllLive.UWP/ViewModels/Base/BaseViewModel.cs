﻿using AllLive.UWP.Helper;
using System;
using System.ComponentModel;
using System.Windows.Input;

namespace AllLive.UWP.ViewModels
{
    public class BaseNotifyPropertyChanged : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public virtual void DoPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    public class BaseViewModel : BaseNotifyPropertyChanged
    {
        public BaseViewModel()
        {
            RefreshCommand = new RelayCommand(Refresh);
            LoadMoreCommand = new RelayCommand(LoadMore);
        }
        public ICommand LoadMoreCommand { get; set; }
        public ICommand RefreshCommand { get; set; }

        public int Page { get; set; } = 1;

        private bool _loading;
        public bool Loading
        {
            get { return _loading; }
            set { _loading = value; DoPropertyChanged("Loading"); }
        }

        private double _loadProgress;
        public double LoadProgress
        {
            get { return _loadProgress; }
            set { _loadProgress = value; DoPropertyChanged(nameof(LoadProgress)); }
        }

        private bool _canLoadMore;
        public bool CanLoadMore
        {
            get { return _canLoadMore; }
            set { _canLoadMore = value; DoPropertyChanged("CanLoadMore"); }
        }



        private bool _empty = false;
        public bool IsEmpty
        {
            get { return _empty; }
            set { _empty = value; DoPropertyChanged("IsEmpty"); }
        }


        public virtual void Refresh()
        {
            Page = 1;
        }
        public virtual void LoadMore()
        {

        }

        public virtual void HandleError(Exception ex, string message = "出现错误，已记录")
        {
            if (LogHelper.IsNetworkError(ex))
            {
                Utils.ShowMessageToast("请检查网络连接情况");
            }
            else
            {
                LogHelper.Log(ex.Message, LogType.ERROR, ex);
                Utils.ShowMessageToast(message);
            }
        }

    }
}
