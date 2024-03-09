using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AllLive.UWP.Models
{
    public class FavoriteItem : INotifyPropertyChanged
    {
        public int ID { get; set; }
        public string RoomID { get; set; }
        public string UserName { get; set; }
        public string Photo { get; set; }
        public string SiteName { get; set; }
        private bool status;
        private string cover;
        private string title;
        public bool Status { get { return status; } set { status = value; notifyPropertyChanged(nameof(Status)); } }
        public string Cover { get { return cover; } set { cover = value; notifyPropertyChanged(nameof(Cover)); } }
        public string Title { get { return title; } set { title = value; notifyPropertyChanged(nameof(Title)); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void notifyPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

    }
}
