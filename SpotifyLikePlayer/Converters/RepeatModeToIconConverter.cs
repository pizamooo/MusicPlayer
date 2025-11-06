using SpotifyLikePlayer.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SpotifyLikePlayer.Converters
{
    public class RepeatModeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is MediaPlayerService.RepeatMode mode)
            {
                switch (mode)
                {
                    case MediaPlayerService.RepeatMode.None:
                        return "Repeat";
                    case MediaPlayerService.RepeatMode.All:
                        return "Repeat";
                    case MediaPlayerService.RepeatMode.One:
                        return "RepeatOnce";
                    default:
                        return "Repeat";
                }
            }
            return "Repeat";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
