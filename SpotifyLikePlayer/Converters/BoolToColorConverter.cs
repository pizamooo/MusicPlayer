using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace SpotifyLikePlayer.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string param && param.Contains(","))
            {
                var colors = param.Split(',');
                var falseColor = colors[0];
                var trueColor = colors[1];

                return (bool)value ?
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString(trueColor)) :
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString(falseColor));
            }
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
