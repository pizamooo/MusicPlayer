using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SpotifyLikePlayer.Models
{
    public class MusicSubmission
    {
        public int SubmissionId { get; set; }
        public int ArtistId { get; set; }
        public string Title { get; set; }
        public string Genre { get; set; }
        public byte[] Mp3File { get; set; }
        public byte[] CoverImagePath { get; set; }
        public string Status { get; set; }         // "Pending", "Approved", "Rejected"
        public DateTime SubmittedAt { get; set; }


        public string ArtistName { get; set; }
        public int ReleaseYear { get; set; }
        public string AlbumTitle { get; set; }
        public string TempFilePath { get; set; }

        public BitmapImage CoverImageSource
        {
            get
            {
                if (CoverImagePath == null || CoverImagePath.Length == 0)
                    return null;

                var image = new BitmapImage();
                using (var ms = new System.IO.MemoryStream(CoverImagePath))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                    image.Freeze();
                }
                return image;
            }
        }
    }
}
