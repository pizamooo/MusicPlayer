using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpotifyLikePlayer.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public byte[] ProfileImage { get; set; }

        public bool IsArtist => Role == "Artist";
        public bool IsAdmin => Role == "Admin";
    }
}
