using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using System.Windows.Media.Imaging;

namespace SpotifyLikePlayer.Models
{
    public class Song 
    {
        public int SongId { get; set; }
        public string Title { get; set; }
        public int ArtistId { get; set; }
        public int AlbumId { get; set; }
        public string FilePath { get; set; }
        public TimeSpan Duration { get; set; }
        public string Genre { get; set; }
        public BitmapImage CoverImage { get; set; }


        public Artist Artist { get; set; }
        public Album Album { get; set; }


        private bool _isFavorite;
        private bool _isFavoriteLocal;
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged();
                    // Синхронизация только если нужно (например, при загрузке из базы)
                    if (_isFavoriteLocal != _isFavorite)
                    {
                        _isFavoriteLocal = _isFavorite;
                        OnPropertyChanged(nameof(IsFavoriteLocal));
                    }
                }
            }
        }

        public bool IsFavoriteLocal
        {
            get => _isFavoriteLocal;
            set
            {
                if (_isFavoriteLocal != value)
                {
                    _isFavoriteLocal = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
