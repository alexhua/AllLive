using System;
using Windows.UI.Xaml.Data;

namespace AllLive.UWP.Converters
{
    public class CountTextConvert : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || (int)value == 0)
            {
                return "";
            }

            return $"({value})";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }
    }
}
