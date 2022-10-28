using AllLive.UWP.Helper;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace AllLive.UWP.ViewModels
{
    public class SettingVM
    {
        public SettingVM()
        {
            LoadShieldSetting();
        }
        public ObservableCollection<string> ShieldWords { get; set; }
        public void LoadShieldSetting()
        {
            ShieldWords = JsonSerializer.Deserialize<ObservableCollection<string>>(SettingHelper.GetValue<string>(SettingHelper.LiveDanmaku.SHIELD_WORD, "[]"));
        }
    }
}
