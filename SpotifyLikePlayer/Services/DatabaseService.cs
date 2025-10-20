using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Data.SqlClient;
using System.Configuration;
using System.Collections.ObjectModel;
using BCrypt.Net;
using Microsoft.IdentityModel.Protocols;
using SpotifyLikePlayer.Models;
using TagLib;
using System.Windows;
using System.IO;
using System.Windows.Media.Imaging;

namespace SpotifyLikePlayer.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString = ConfigurationManager.ConnectionStrings["MusicDB"].ConnectionString;

        public User Authenticate(string username, string password)
        {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string query = "SELECT * FROM Users WHERE Username = @Username";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Username", username);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string hash = reader["PasswordHash"].ToString();
                                    if (BCrypt.Net.BCrypt.Verify(password, hash))
                                    {
                                        return new User
                                        {
                                            UserId = (int)reader["UserId"],
                                            Username = username,
                                            Email = reader["Email"].ToString()
                                        };
                                    }
                            }
                        }
                    }
                }
                return null;
        }

        public bool RegisterUser(string username, string password, string email)
        {
            // Проверяем, существует ли пользователь с таким username или email
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string checkQuery = "SELECT COUNT(*) FROM Users WHERE Username = @Username OR Email = @Email";
                using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@Username", username);
                    checkCmd.Parameters.AddWithValue("@Email", email);
                    int count = (int)checkCmd.ExecuteScalar();
                    if (count > 0)
                    {
                        return false; // Пользователь уже существует
                    }
                }

                // Если не существует, хэшируем пароль и вставляем
                string hash = BCrypt.Net.BCrypt.HashPassword(password);
                string insertQuery = "INSERT INTO Users (Username, PasswordHash, Email) VALUES (@Username, @Hash, @Email)";
                using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                {
                    insertCmd.Parameters.AddWithValue("@Username", username);
                    insertCmd.Parameters.AddWithValue("@Hash", hash);
                    insertCmd.Parameters.AddWithValue("@Email", email);
                    insertCmd.ExecuteNonQuery();
                }
                return true; // Регистрация успешна
            }
        }

        public bool ResetPassword(string email, string newPassword)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                // Проверяем существование пользователя
                string checkQuery = "SELECT COUNT(*) FROM Users WHERE Email = @Email";
                using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@Email", email);
                    int count = (int)checkCmd.ExecuteScalar();
                    if (count == 0)
                    {
                        return false; // Пользователь не найден
                    }
                }

                // Обновляем пароль
                string hash = BCrypt.Net.BCrypt.HashPassword(newPassword);
                string updateQuery = "UPDATE Users SET PasswordHash = @Hash WHERE Email = @Email";
                using (SqlCommand updateCmd = new SqlCommand(updateQuery, conn))
                {
                    updateCmd.Parameters.AddWithValue("@Hash", hash);
                    updateCmd.Parameters.AddWithValue("@Email", email);
                    updateCmd.ExecuteNonQuery();
                }
                return true;
            }
        }

        public ObservableCollection<Song> GetSongs()
        {
            ObservableCollection<Song> songs = new ObservableCollection<Song>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = @"
                    SELECT s.SongId, s.Title, s.FilePath, s.Duration, s.Genre,
                           a.ArtistId, a.Name AS ArtistName,
                           al.AlbumId, al.Title AS AlbumTitle, al.ReleaseYear
                    FROM Songs s
                    JOIN Artists a ON s.ArtistId = a.ArtistId
                    JOIN Albums al ON s.AlbumId = al.AlbumId";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            TimeSpan duration;
                            int durationOrdinal = reader.GetOrdinal("Duration");
                            if (!reader.IsDBNull(durationOrdinal))
                            {
                                duration = reader.GetTimeSpan(durationOrdinal);
                            }
                            else
                            {
                                duration = TimeSpan.Zero; // Если NULL, устанавливаем 0
                            }

                            var song = new Song
                            {
                                SongId = (int)reader["SongId"],
                                Title = reader["Title"].ToString(),
                                FilePath = reader["FilePath"].ToString(),
                                Duration = duration,
                                Genre = reader["Genre"].ToString(),
                                ArtistId = (int)reader["ArtistId"],
                                Artist = new Artist { ArtistId = (int)reader["ArtistId"], Name = reader["ArtistName"].ToString() },
                                AlbumId = (int)reader["AlbumId"],
                                Album = new Album { AlbumId = (int)reader["AlbumId"], Title = reader["AlbumTitle"].ToString(), ReleaseYear = (int)reader["ReleaseYear"] }
                            };
                            song.CoverImage = GetCoverImage(song.FilePath);  // обложка
                            songs.Add(song);
                        }
                    }
                }
            }
            return songs;
        }

        public ObservableCollection<Playlist> GetPlaylists(int userId)
        {
            ObservableCollection<Playlist> playlists = new ObservableCollection<Playlist>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = "SELECT PlaylistId, Name, UserId, CreatedDate FROM Playlists WHERE UserId = @UserId";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            playlists.Add(new Playlist
                            {
                                PlaylistId = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                UserId = reader.GetInt32(2),
                                CreatedDate = reader.GetDateTime(3)
                            });
                        }
                    }
                }
            }
            return playlists;
        }

        public ObservableCollection<Song> GetPlaylistSongs(int playlistId)
        {
            ObservableCollection<Song> songs = new ObservableCollection<Song>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = @"
                SELECT s.SongId, s.Title, s.FilePath, s.Duration, s.Genre, 
                       a.ArtistId, a.Name AS ArtistName,
                       al.AlbumId, al.Title AS AlbumTitle, al.ReleaseYear,
                       ps.Position
                FROM PlaylistSongs ps
                JOIN Songs s ON ps.SongId = s.SongId
                JOIN Artists a ON s.ArtistId = a.ArtistId
                JOIN Albums al ON s.AlbumId = al.AlbumId
                WHERE ps.PlaylistId = @PlaylistId
                ORDER BY ps.Position";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PlaylistId", playlistId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            songs.Add(new Song
                            {
                                SongId = reader.GetInt32(reader.GetOrdinal("SongId")),
                                Title = reader.GetString(reader.GetOrdinal("Title")),
                                FilePath = reader.GetString(reader.GetOrdinal("FilePath")),
                                Duration = reader.GetTimeSpan(reader.GetOrdinal("Duration")),
                                Genre = reader.IsDBNull(reader.GetOrdinal("Genre")) ? null : reader.GetString(reader.GetOrdinal("Genre")),
                                ArtistId = reader.GetInt32(reader.GetOrdinal("ArtistId")),
                                Artist = new Artist { ArtistId = reader.GetInt32(reader.GetOrdinal("ArtistId")), Name = reader.GetString(reader.GetOrdinal("ArtistName")) },
                                AlbumId = reader.GetInt32(reader.GetOrdinal("AlbumId")),
                                Album = new Album { AlbumId = reader.GetInt32(reader.GetOrdinal("AlbumId")), Title = reader.GetString(reader.GetOrdinal("AlbumTitle")), ReleaseYear = reader.GetInt32(reader.GetOrdinal("ReleaseYear")) },
                                CoverImage = GetCoverImage(reader.GetString(reader.GetOrdinal("FilePath")))
                            });
                        }
                    }
                }
            }
            return songs;
        }

        public bool SongExists(string filePath)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = "SELECT COUNT(*) FROM Songs WHERE FilePath = @FilePath";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@FilePath", filePath);
                    return (int)cmd.ExecuteScalar() > 0;
                }
            }
        }

        public int AddOrGetArtist(string artistName)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string checkQuery = "SELECT ArtistId FROM Artists WHERE Name = @Name";
                using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@Name", artistName);
                    object id = checkCmd.ExecuteScalar();
                    if (id != null) return (int)id;
                }

                string insertQuery = "INSERT INTO Artists (Name) OUTPUT INSERTED.ArtistId VALUES (@Name)";
                using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                {
                    insertCmd.Parameters.AddWithValue("@Name", artistName);
                    return (int)insertCmd.ExecuteScalar();
                }
            }
        }

        public int AddOrGetAlbum(string albumTitle, int artistId = 1, int releaseYear = 0)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string checkQuery = "SELECT AlbumId FROM Albums WHERE Title = @Title AND ArtistId = @ArtistId";
                using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@Title", albumTitle);
                    checkCmd.Parameters.AddWithValue("@ArtistId", artistId);
                    object id = checkCmd.ExecuteScalar();
                    if (id != null) return (int)id;
                }

                string insertQuery = "INSERT INTO Albums (Title, ArtistId, ReleaseYear) OUTPUT INSERTED.AlbumId VALUES (@Title, @ArtistId, @ReleaseYear)";
                using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                {
                    insertCmd.Parameters.AddWithValue("@Title", albumTitle);
                    insertCmd.Parameters.AddWithValue("@ArtistId", artistId);
                    insertCmd.Parameters.AddWithValue("@ReleaseYear", releaseYear);
                    return (int)insertCmd.ExecuteScalar();
                }
            }
        }

        public void AddSong(Song song)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = @"
            INSERT INTO Songs (Title, ArtistId, AlbumId, FilePath, Duration, Genre)
            VALUES (@Title, @ArtistId, @AlbumId, @FilePath, @Duration, @Genre)";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Title", song.Title);
                    cmd.Parameters.AddWithValue("@ArtistId", song.ArtistId);
                    cmd.Parameters.AddWithValue("@AlbumId", song.AlbumId);
                    cmd.Parameters.AddWithValue("@FilePath", song.FilePath);
                    cmd.Parameters.AddWithValue("@Duration",song.Duration.ToString("mm\\:ss"));  // TimeSpan как time
                    cmd.Parameters.AddWithValue("@Genre", song.Genre);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public BitmapImage GetCoverImage(string filePath)
        {
            if (!System.IO.File.Exists(filePath)) return GetDefaultCover();  // Дефолт, если файл не найден

            try
            {
                using (var tagFile = TagLib.File.Create(filePath))
                {
                    if (tagFile.Tag.Pictures.Length > 0)
                    {
                        var picture = tagFile.Tag.Pictures[0];
                        using (var stream = new MemoryStream(picture.Data.Data))
                        {
                            var image = new BitmapImage();
                            image.BeginInit();
                            image.CacheOption = BitmapCacheOption.OnLoad;
                            image.StreamSource = stream;
                            image.EndInit();
                            image.Freeze();  // Для thread-safety в WPF
                            return image;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ошибка, вернём дефолт
            }
            return GetDefaultCover();
        }

        public BitmapImage GetDefaultCover()
        {
            var defaultImage = new BitmapImage(new Uri("C:\\Users\\dobry\\source\\repos\\SpotifyLikePlayer\\SpotifyLikePlayer\\music.png"));  // Путь к ресурсу
            return defaultImage;
        }

        public Playlist GetOrCreateFavoritePlaylist(int userId)
        {
            Playlist favoritePlaylist = null;
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = "SELECT PlaylistId, Name, UserId, CreatedDate FROM Playlists WHERE Name = 'Любимое' AND UserId = @UserId";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            favoritePlaylist = new Playlist
                            {
                                PlaylistId = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                UserId = reader.GetInt32(2),
                                CreatedDate = reader.GetDateTime(3)
                            };
                            favoritePlaylist.Songs = GetPlaylistSongs(favoritePlaylist.PlaylistId);
                        }
                    }
                }

                if (favoritePlaylist == null)
                {
                    string insertQuery = "INSERT INTO Playlists (Name, UserId, CreatedDate) OUTPUT INSERTED.PlaylistId VALUES ('Любимое', @UserId, GETDATE())";
                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@UserId", userId);
                        int newId = (int)insertCmd.ExecuteScalar();
                        favoritePlaylist = new Playlist { PlaylistId = newId, Name = "Любимое", UserId = userId, CreatedDate = DateTime.Now };
                    }
                }
            }
            return favoritePlaylist;
        }

        public void AddSongToPlaylist(int playlistId, int songId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string checkQuery = "SELECT COUNT(*) FROM PlaylistSongs WHERE PlaylistId = @PlaylistId AND SongId = @SongId";
                using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@PlaylistId", playlistId);
                    checkCmd.Parameters.AddWithValue("@SongId", songId);
                    int count = (int)checkCmd.ExecuteScalar();
                    if (count > 0) return;
                }

                string getPositionQuery = "SELECT ISNULL(MAX(Position), 0) + 1 FROM PlaylistSongs WHERE PlaylistId = @PlaylistId";
                using (SqlCommand getPosCmd = new SqlCommand(getPositionQuery, conn))
                {
                    getPosCmd.Parameters.AddWithValue("@PlaylistId", playlistId);
                    int position = (int)getPosCmd.ExecuteScalar();

                    string insertQuery = "INSERT INTO PlaylistSongs (PlaylistId, SongId, Position) VALUES (@PlaylistId, @SongId, @Position)";
                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@PlaylistId", playlistId);
                        insertCmd.Parameters.AddWithValue("@SongId", songId);
                        insertCmd.Parameters.AddWithValue("@Position", position);
                        insertCmd.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}
