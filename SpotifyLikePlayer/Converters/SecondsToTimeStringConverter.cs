using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SpotifyLikePlayer.Converters
{
    public class SecondsToTimeStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double seconds)
            {
                TimeSpan time = TimeSpan.FromSeconds(seconds);
                return time.ToString("mm\\:ss");
            }
            return "00:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
