using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SpotifyLikePlayer.Converters
{
    public class ProgressWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3)
                return 0;

            double totalWidth = (double)values[0];
            double value = System.Convert.ToDouble(values[1]);
            double maximum = System.Convert.ToDouble(values[2]);

            if (maximum <= 0) return 0;

            // Учитываем отступы (в твоём случае Margin="8,0")
            double width = (totalWidth - 16) * (value / maximum);
            return width < 0 ? 0 : width;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
