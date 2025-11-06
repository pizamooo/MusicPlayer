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
        public readonly string _connectionString = ConfigurationManager.ConnectionStrings["MusicDB"].ConnectionString;

        public ObservableCollection<Song> GetSongsByGenre(string genre)
        {
            var songs = new ObservableCollection<Song>();

            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                string query = @"
            SELECT s.SongId, s.Title, s.FilePath, s.Duration, s.Genre,
                   a.ArtistId, a.Name AS ArtistName,
                   al.AlbumId, al.Title AS AlbumTitle, al.ReleaseYear
            FROM Songs s
            JOIN Artists a ON s.ArtistId = a.ArtistId
            JOIN Albums al ON s.AlbumId = al.AlbumId
            WHERE s.Genre = @Genre";  // фильтрация по жанру

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Genre", genre);

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
                                duration = TimeSpan.Zero;
                            }

                            var song = new Song
                            {
                                SongId = (int)reader["SongId"],
                                Title = reader["Title"].ToString(),
                                FilePath = reader["FilePath"].ToString(),
                                Duration = duration,
                                Genre = reader["Genre"].ToString(),
                                ArtistId = (int)reader["ArtistId"],
                                Artist = new Artist
                                {
                                    ArtistId = (int)reader["ArtistId"],
                                    Name = reader["ArtistName"].ToString()
                                },
                                AlbumId = (int)reader["AlbumId"],
                                Album = new Album
                                {
                                    AlbumId = (int)reader["AlbumId"],
                                    Title = reader["AlbumTitle"].ToString(),
                                    ReleaseYear = (int)reader["ReleaseYear"]
                                }
                            };

                            song.CoverImage = GetCoverImage(song.FilePath);
                            songs.Add(song);
                        }
                    }
                }
            }

            return songs;
        }

        public ObservableCollection<Playlist> GetPlaylists(int? userId = null)
        {
            var playlists = new ObservableCollection<Playlist>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                string query = userId.HasValue
                    ? "SELECT PlaylistId, Name, UserId, CreatedDate FROM Playlists WHERE UserId = @UserId"
                    : "SELECT PlaylistId, Name, UserId, CreatedDate FROM Playlists";

                using (var cmd = new SqlCommand(query, conn))
                {
                    if (userId.HasValue)
                        cmd.Parameters.AddWithValue("@UserId", userId.Value);

                    var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        playlists.Add(new Playlist
                        {
                            PlaylistId = (int)reader["PlaylistId"],
                            Name = reader["Name"].ToString(),
                            UserId = reader["UserId"] is DBNull ? 0 : (int)reader["UserId"],
                            CreatedDate = reader["CreatedDate"] is DBNull ? DateTime.Now : (DateTime)reader["CreatedDate"]
                        });
                    }
                }
            }

            return playlists;
        }

        public Playlist CreatePlaylist(string name, int userId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string insertQuery = @"
                    INSERT INTO Playlists (Name, UserId, CreatedDate) 
                    VALUES (@Name, @UserId, GETDATE());
                    SELECT SCOPE_IDENTITY();";

                using (var cmd = new SqlCommand(insertQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@UserId", userId);

                    int id = Convert.ToInt32(cmd.ExecuteScalar());
                    return new Playlist
                    {
                        PlaylistId = id,
                        Name = name,
                        UserId = userId,
                        CreatedDate = DateTime.Now
                    };
                }
            }
        }

        public void DeletePlaylist(int playlistId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                var cmdSongs = new SqlCommand("DELETE FROM PlaylistSongs WHERE PlaylistId = @PlaylistId", conn);
                cmdSongs.Parameters.AddWithValue("@PlaylistId", playlistId);
                cmdSongs.ExecuteNonQuery();

                var cmdPlaylist = new SqlCommand("DELETE FROM Playlists WHERE PlaylistId = @PlaylistId", conn);
                cmdPlaylist.Parameters.AddWithValue("@PlaylistId", playlistId);
                cmdPlaylist.ExecuteNonQuery();
            }
        }

        public void AddSongToPlaylist(int playlistId, int songId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                string query = @"
                    INSERT INTO PlaylistSongs (PlaylistId, SongId, Position)
                    VALUES (@PlaylistId, @SongId,
                    (SELECT ISNULL(MAX(Position), 0) + 1 FROM PlaylistSongs WHERE PlaylistId = @PlaylistId))";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PlaylistId", playlistId);
                    cmd.Parameters.AddWithValue("@SongId", songId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public bool IsSongInPlaylist(int playlistId, int songId)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                string query = "SELECT COUNT(*) FROM PlaylistSongs WHERE PlaylistId=@p AND SongId=@s";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@p", playlistId);
                    cmd.Parameters.AddWithValue("@s", songId);
                    return (int)cmd.ExecuteScalar() > 0;
                }
            }
        }

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
            var songs = new ObservableCollection<Song>();
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
                                Album = new Album { AlbumId = (int)reader["AlbumId"], Title = reader["AlbumTitle"].ToString(), ReleaseYear = (int)reader["ReleaseYear"] },
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
                string query = "SELECT PlaylistId, Name, CreatedDate FROM Playlists WHERE UserId = @UserId";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            playlists.Add(new Playlist
                            {
                                PlaylistId = (int)reader["PlaylistId"],
                                Name = reader["Name"].ToString(),
                                UserId = userId,
                                CreatedDate = (DateTime)reader["CreatedDate"]
                            });
                        }
                    }
                }
            }
            return playlists;
        }

        public ObservableCollection<Song> GetPlaylistSongs(int playlistId)
        {
            Console.WriteLine($"Запрос песен для плейлиста ID: {playlistId}");
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
            ORDER BY CAST(ps.Position AS INT)";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@PlaylistId", playlistId);
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
                                duration = TimeSpan.Zero;
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
                            song.CoverImage = GetCoverImage(song.FilePath);
                            songs.Add(song);
                        }
                    }
                }
            }
            Console.WriteLine($"Загружено песен: {songs.Count}");
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

                string checkQuery = "SELECT AlbumId, ReleaseYear FROM Albums WHERE Title = @Title AND ArtistId = @ArtistId";
                using (var checkCmd = new SqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@Title", albumTitle);
                    checkCmd.Parameters.AddWithValue("@ArtistId", artistId);

                    using (var reader = checkCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int albumId = (int)reader["AlbumId"];
                            int existingYear = reader["ReleaseYear"] is DBNull ? 0 : (int)reader["ReleaseYear"];
                            reader.Close();

                            if (existingYear == 0 && releaseYear > 0)
                            {
                                string updateQuery = "UPDATE Albums SET ReleaseYear = @ReleaseYear WHERE AlbumId = @AlbumId";
                                using (var updateCmd = new SqlCommand(updateQuery, conn))
                                {
                                    updateCmd.Parameters.AddWithValue("@ReleaseYear", releaseYear);
                                    updateCmd.Parameters.AddWithValue("@AlbumId", albumId);
                                    updateCmd.ExecuteNonQuery();
                                }
                            }

                            return albumId;
                        }
                    }
                }

                string insertQuery = @"
            INSERT INTO Albums (Title, ArtistId, ReleaseYear)
            OUTPUT INSERTED.AlbumId
            VALUES (@Title, @ArtistId, @ReleaseYear)";

                using (var insertCmd = new SqlCommand(insertQuery, conn))
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

                int releaseYear = 0;
                string genre = song.Genre;
                string albumTitle = song.Album?.Title;

                try
                {
                    using (var tagFile = TagLib.File.Create(song.FilePath))
                    {
                        if (tagFile.Tag.Year != 0)
                            releaseYear = (int)tagFile.Tag.Year;

                        if (!string.IsNullOrWhiteSpace(tagFile.Tag.FirstGenre))
                            genre = tagFile.Tag.FirstGenre;

                        if (string.IsNullOrWhiteSpace(albumTitle))
                            albumTitle = tagFile.Tag.Album;
                    }
                }
                catch
                {

                }

                if (string.IsNullOrWhiteSpace(albumTitle))
                    albumTitle = "Single";

                int artistId = AddOrGetArtist(song.Artist?.Name ?? "Unknown Artist");

                int albumId = AddOrGetAlbum(albumTitle, artistId, releaseYear);

                string query = @"
            INSERT INTO Songs (Title, ArtistId, AlbumId, FilePath, Duration, Genre)
            VALUES (@Title, @ArtistId, @AlbumId, @FilePath, @Duration, @Genre)";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Title", song.Title);
                    cmd.Parameters.AddWithValue("@ArtistId", artistId);
                    cmd.Parameters.AddWithValue("@AlbumId", albumId);
                    cmd.Parameters.AddWithValue("@FilePath", song.FilePath);
                    cmd.Parameters.AddWithValue("@Duration", song.Duration);
                    cmd.Parameters.AddWithValue("@Genre", genre ?? "Unknown");
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
                            image.Freeze();
                            return image;
                        }
                    }
                }
            }
            catch (Exception)
            {
                
            }
            return GetDefaultCover();
        }

        public BitmapImage GetDefaultCover()
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri("C:\\Users\\dobry\\source\\repos\\SpotifyLikePlayer\\SpotifyLikePlayer\\music.png", UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }
    }
}
