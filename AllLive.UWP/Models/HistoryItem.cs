using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AllLive.UWP.Models
{
    public class HistoryItem : INotifyPropertyChanged
    {
        public int ID { get; set; }
        public string RoomID { get; set; }
        public string UserName { get; set; }
        public string Photo { get; set; }
        public string SiteName { get; set; }
        public DateTime WatchTime { get; set; }
        private bool status;
        public bool Status { get { return status; } set { status = value; notifyPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void notifyPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
