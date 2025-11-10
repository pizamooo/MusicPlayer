using SpotifyLikePlayer.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpotifyLikePlayer.Services
{
    public class MusicSubmissionService
    {
        private readonly string _connectionString = ConfigurationManager.ConnectionStrings["MusicDB"].ConnectionString;

        public void SubmitMusicRequest(int artistId, string title, string genre, string mp3FilePath, byte[] coverImage = null, int releaseYear = 0)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = @"
            INSERT INTO MusicSubmissions (ArtistId, Title, Genre, Mp3FilePath, CoverImagePath, ReleaseYear, Status)
            VALUES (@ArtistId, @Title, @Genre, @Mp3FilePath, @CoverImage, @ReleaseYear, 'Pending')";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ArtistId", artistId);
                    cmd.Parameters.AddWithValue("@Title", title);
                    cmd.Parameters.AddWithValue("@Genre", genre ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Mp3FilePath", mp3FilePath);
                    cmd.Parameters.AddWithValue("@CoverImage", coverImage ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ReleaseYear", releaseYear);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public ObservableCollection<MusicSubmission> GetPendingSubmissions()
        {
            var list = new ObservableCollection<MusicSubmission>();
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = "SELECT * FROM MusicSubmissions WHERE Status = 'Pending'";
                using (var cmd = new SqlCommand(query, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new MusicSubmission
                        {
                            SubmissionId = (int)reader["SubmissionId"],
                            ArtistId = (int)reader["ArtistId"],
                            Title = reader["Title"].ToString(),
                            Genre = reader["Genre"]?.ToString(),
                            Mp3File = (byte[])reader["Mp3FilePath"],
                            CoverImage = reader["CoverImagePath"] as byte[],
                            Status = reader["Status"].ToString(),
                            SubmittedAt = (DateTime)reader["SubmittedAt"]
                        });
                    }
                }
            }
            return list;
        }

        public void ApproveSubmission(int submissionId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var sub = GetSubmissionById(submissionId, conn);
                if (sub == null) return;

                int artistId = sub.ArtistId;
                int albumId = AddOrGetAlbum("Single", artistId, DateTime.Now.Year, conn);

                string insertSong = @"
                INSERT INTO Songs (Title, ArtistId, AlbumId, FilePath, Duration, Genre)
                VALUES (@Title, @ArtistId, @AlbumId, @FilePath, @Duration, @Genre)";
                using (var cmd = new SqlCommand(insertSong, conn))
                {
                    cmd.Parameters.AddWithValue("@Title", sub.Title);
                    cmd.Parameters.AddWithValue("@ArtistId", artistId);
                    cmd.Parameters.AddWithValue("@AlbumId", albumId);
                    cmd.Parameters.AddWithValue("@FilePath", sub.Mp3File);
                    cmd.Parameters.AddWithValue("@Duration", TimeSpan.Zero);
                    cmd.Parameters.AddWithValue("@Genre", sub.Genre ?? "Unknown");
                    cmd.ExecuteNonQuery();
                }

                string update = "UPDATE MusicSubmissions SET Status = 'Approved' WHERE SubmissionId = @Id";
                using (var cmd = new SqlCommand(update, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", submissionId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private MusicSubmission GetSubmissionById(int id, SqlConnection conn)
        {
            string query = "SELECT * FROM MusicSubmissions WHERE SubmissionId = @Id";
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return new MusicSubmission
                        {
                            SubmissionId = (int)reader["SubmissionId"],
                            ArtistId = (int)reader["ArtistId"],
                            Title = reader["Title"].ToString(),
                            Genre = reader["Genre"]?.ToString(),
                            Mp3File = (byte[])reader["Mp3FilePath"],
                            CoverImage = reader["CoverImagePath"] as byte[],
                            Status = reader["Status"].ToString(),
                            SubmittedAt = (DateTime)reader["SubmittedAt"]
                        };
                    }
                }
            }
            return null;
        }

        private int AddOrGetAlbum(string title, int artistId, int year, SqlConnection conn)
        {
            string check = "SELECT AlbumId FROM Albums WHERE Title = @Title AND ArtistId = @ArtistId";
            using (var cmd = new SqlCommand(check, conn))
            {
                cmd.Parameters.AddWithValue("@Title", title);
                cmd.Parameters.AddWithValue("@ArtistId", artistId);
                object id = cmd.ExecuteScalar();
                if (id != null) return (int)id;
            }

            string insert = "INSERT INTO Albums (Title, ArtistId, ReleaseYear) OUTPUT INSERTED.AlbumId VALUES (@Title, @ArtistId, @Year)";
            using (var cmd = new SqlCommand(insert, conn))
            {
                cmd.Parameters.AddWithValue("@Title", title);
                cmd.Parameters.AddWithValue("@ArtistId", artistId);
                cmd.Parameters.AddWithValue("@Year", year);
                return (int)cmd.ExecuteScalar();
            }
        }
    }
}
