using SpotifyLikePlayer.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpotifyLikePlayer.Services
{
    public class MusicSubmissionService
    {
        private readonly string _connectionString = ConfigurationManager.ConnectionStrings["MusicDB"].ConnectionString;

        public string SaveTempFile(MusicSubmission sub)
        {
            try
            {
                string tempFolder = Path.Combine(Path.GetTempPath(), "SpotifyLikePlayer_MusicSubmissions");
                if (!Directory.Exists(tempFolder))
                    Directory.CreateDirectory(tempFolder);

                string fileName = $"{sub.Title}_{sub.SubmissionId}_{DateTime.Now:yyyyMMddHHmmss}.mp3";
                string fullPath = Path.Combine(tempFolder, fileName);

                File.WriteAllBytes(fullPath, sub.Mp3File);

                sub.TempFilePath = fullPath;

                return fullPath;
            }
            catch
            {
                return null;
            }
        }

        public void SubmitMusicRequest(int artistId, string title, string genre, byte[] mp3File, byte[] coverImage = null, int releaseYear = 0, string albumTitle = null)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = @"
            INSERT INTO MusicSubmissions (ArtistId, Title, Genre, Mp3FilePath, CoverImagePath, ReleaseYear, Status, AlbumTitle)
            VALUES (@ArtistId, @Title, @Genre, @Mp3File, @CoverImage, @ReleaseYear, 'Pending', @AlbumTitle)";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ArtistId", artistId);
                    cmd.Parameters.AddWithValue("@Title", title);
                    cmd.Parameters.AddWithValue("@Genre", genre ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Mp3File", mp3File);
                    cmd.Parameters.AddWithValue("@CoverImage", coverImage ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@ReleaseYear", releaseYear);
                    cmd.Parameters.AddWithValue("@AlbumTitle", albumTitle ?? (object)DBNull.Value); // 🔥 Новое поле
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public ObservableCollection<MusicSubmission> GetSubmissionsByStatus(string status)
        {
            var list = new ObservableCollection<MusicSubmission>();
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = @"
            SELECT ms.SubmissionId, ms.Title, ms.Genre, ms.Mp3FilePath, ms.CoverImagePath, ms.Status, ms.SubmittedAt,
                   u.Username AS ArtistName, ms.ReleaseYear, ms.AlbumTitle
            FROM MusicSubmissions ms
            JOIN Users u ON ms.ArtistId = u.UserId
            WHERE ms.Status = @Status";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Status", status);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new MusicSubmission
                            {
                                SubmissionId = (int)reader["SubmissionId"],
                                Title = reader["Title"].ToString(),
                                Genre = reader["Genre"]?.ToString(),
                                Mp3File = reader["Mp3FilePath"] as byte[],
                                CoverImagePath = reader["CoverImagePath"] as byte[],
                                Status = reader["Status"].ToString(),
                                SubmittedAt = (DateTime)reader["SubmittedAt"],
                                ArtistName = reader["ArtistName"].ToString(),
                                ReleaseYear = reader["ReleaseYear"] is DBNull ? 0 : (int)reader["ReleaseYear"],
                                AlbumTitle = reader["AlbumTitle"] as string
                            });
                        }
                    }
                }
            }
            return list;
        }

        public void DeleteSubmission(int submissionId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = "DELETE FROM MusicSubmissions WHERE SubmissionId = @Id";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", submissionId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteAllSubmissionsByStatus(string status)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = "DELETE FROM MusicSubmissions WHERE Status = @Status";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Status", status);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void ApproveSubmission(int submissionId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var sub = GetSubmissionById(submissionId, conn);
                if (sub == null)
                {
                    throw new InvalidOperationException("Заявка не найдена.");
                }

                string artistName = GetArtistNameFromUser(sub.ArtistId, conn);

                int artistId = AddOrGetArtist(artistName, conn);

                string albumTitle = string.IsNullOrWhiteSpace(sub.AlbumTitle) ? $"{artistName} - Single {(sub.ReleaseYear > 0 ? sub.ReleaseYear : DateTime.Now.Year)}" : sub.AlbumTitle;

                if (IsSongAlreadyExists(sub.Title, artistId, conn))
                {
                    throw new InvalidOperationException($"Песня '{sub.Title}' артиста '{artistName}' уже существует в библиотеке.");
                }

                int albumId = AddOrGetAlbum(albumTitle, artistId, sub.ReleaseYear > 0 ? sub.ReleaseYear : DateTime.Now.Year, conn);

                var (filePath, duration) = SaveMp3File(sub.Mp3File, sub.Title);
                if (string.IsNullOrEmpty(filePath))
                {
                    throw new InvalidOperationException("Не удалось сохранить MP3-файл на диск.");
                }

                string insertSong = @"
            INSERT INTO Songs (Title, ArtistId, AlbumId, FilePath, Duration, Genre, CoverImage)
            VALUES (@Title, @ArtistId, @AlbumId, @FilePath, @Duration, @Genre, @CoverImage)";

                using (var cmd = new SqlCommand(insertSong, conn))
                {
                    cmd.Parameters.AddWithValue("@Title", sub.Title);
                    cmd.Parameters.AddWithValue("@ArtistId", artistId);
                    cmd.Parameters.AddWithValue("@AlbumId", albumId);
                    cmd.Parameters.AddWithValue("@FilePath", filePath);
                    cmd.Parameters.AddWithValue("@Duration", duration);
                    cmd.Parameters.AddWithValue("@Genre", sub.Genre ?? "Unknown");
                    cmd.Parameters.AddWithValue("@CoverImage", sub.CoverImagePath ?? (object)DBNull.Value);
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

        private int AddOrGetArtist(string artistName, SqlConnection conn)
        {
            string check = "SELECT ArtistId FROM Artists WHERE Name = @Name";
            using (var cmd = new SqlCommand(check, conn))
            {
                cmd.Parameters.AddWithValue("@Name", artistName);
                object id = cmd.ExecuteScalar();
                if (id != null) return (int)id;
            }

            string insert = "INSERT INTO Artists (Name) OUTPUT INSERTED.ArtistId VALUES (@Name)";
            using (var cmd = new SqlCommand(insert, conn))
            {
                cmd.Parameters.AddWithValue("@Name", artistName);
                return (int)cmd.ExecuteScalar();
            }
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

        private string GetArtistNameFromUser(int userId, SqlConnection conn)
        {
            string query = "SELECT Username FROM Users WHERE UserId = @UserId";
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                object name = cmd.ExecuteScalar();
                return name?.ToString() ?? "Unknown Artist";
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
                            Mp3File = reader["Mp3FilePath"] as byte[],
                            CoverImagePath = reader["CoverImagePath"] as byte[],
                            Status = reader["Status"].ToString(),
                            SubmittedAt = (DateTime)reader["SubmittedAt"],
                            AlbumTitle = reader["AlbumTitle"] as string
                        };
                    }
                }
            }
            return null;
        }
        private (string path, TimeSpan duration) SaveMp3File(byte[] mp3Bytes, string title)
        {
            try
            {
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ApprovedMusic");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                string fileName = $"{title}_{DateTime.Now:yyyyMMddHHmmss}.mp3";
                string path = Path.Combine(folder, fileName);

                File.WriteAllBytes(path, mp3Bytes);

                using (var file = TagLib.File.Create(path))
                {
                    TimeSpan duration = file.Properties.Duration;
                    return (path, duration);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка сохранения MP3: {ex.Message}");
                return (null, TimeSpan.Zero);
            }
        }

        public void RejectSubmission(int submissionId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string update = "UPDATE MusicSubmissions SET Status = 'Rejected' WHERE SubmissionId = @Id";
                using (var cmd = new SqlCommand(update, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", submissionId);
                    cmd.ExecuteNonQuery();
                }
            }
        }


        private bool IsSongAlreadyExists(string title, int artistId, SqlConnection conn)
        {
            string query = "SELECT COUNT(*) FROM Songs WHERE Title = @Title AND ArtistId = @ArtistId";
            using (var cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Title", title);
                cmd.Parameters.AddWithValue("@ArtistId", artistId);
                int count = (int)cmd.ExecuteScalar();
                return count > 0;
            }
        }
    }
}
