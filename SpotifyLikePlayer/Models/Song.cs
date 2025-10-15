using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public string CoverImagePath { get; set; }


        public Artist Artist { get; set; }
        public Album Album { get; set; }
    }
}
