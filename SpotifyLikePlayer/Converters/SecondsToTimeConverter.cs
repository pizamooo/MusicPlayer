using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using System.Windows.Data;

namespace SpotifyLikePlayer.Converters
{
    public class SecondsToTimeConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2 || !(values[0] is double current) || !(values[1] is double total))
            {
                return "00:00 / 00:00";
            }

            TimeSpan currentTime = TimeSpan.FromSeconds(current);
            TimeSpan totalTime = TimeSpan.FromSeconds(total);
            return $"{currentTime.ToString("mm\\:ss")} / {totalTime.ToString("mm\\:ss")}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
