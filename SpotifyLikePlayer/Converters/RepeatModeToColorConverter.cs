using SpotifyLikePlayer.Services;
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
    public class RepeatModeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MediaPlayerService.RepeatMode mode)
            {
                // Если не None — активный (зелёный)
                bool isActive = mode != MediaPlayerService.RepeatMode.None;
                return isActive ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1DB954"))
                                : new SolidColorBrush(Colors.White);
            }
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
