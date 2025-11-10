using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpotifyLikePlayer.Models
{
    public class MusicSubmission
    {
        public int SubmissionId { get; set; }
        public int ArtistId { get; set; }
        public string Title { get; set; }
        public string Genre { get; set; }
        public byte[] Mp3File { get; set; }
        public byte[] CoverImage { get; set; }
        public string Status { get; set; }         // "Pending", "Approved", "Rejected"
        public DateTime SubmittedAt { get; set; }
    }
}
