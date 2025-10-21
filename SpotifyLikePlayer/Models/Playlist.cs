using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpotifyLikePlayer.Models
{
    public class Playlist
    {
        public int PlaylistId { get; set; }
        public string Name { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedDate { get; set; }
        public ObservableCollection<Song> Songs { get; set; }

        public Playlist()
        {
            Songs = new ObservableCollection<Song>();
        }
    }
}
