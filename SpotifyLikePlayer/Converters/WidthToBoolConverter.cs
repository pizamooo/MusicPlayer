using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SpotifyLikePlayer.Converters
{
    public class WidthToBoolConverter : IValueConverter
    {
        public double Threshold { get; set; } = 500;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double actualWidth)
            {
                return actualWidth >= Threshold;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
