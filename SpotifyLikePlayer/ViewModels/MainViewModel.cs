using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using SpotifyLikePlayer.Models;
using SpotifyLikePlayer.Services;
using System.Windows;
using System.IO;
using TagLib;
using MaterialDesignThemes.Wpf;
using System.Windows.Threading;
using System.Data.SqlClient;

namespace SpotifyLikePlayer.ViewModels
{
    public class MainViewModel
    {
        public DatabaseService _dbService = new DatabaseService();
        public MediaPlayerService _playerService = new MediaPlayerService();
        private const string DefaultMusicPath = @"C:\Users\dobry\OneDrive\Documents\MusicForProject";  // путь песен

        public event PropertyChangedEventHandler PropertyChanged;

        private ObservableCollection<Song> _songs = new ObservableCollection<Song>();
        public ObservableCollection<Song> Songs
        {
            get => _songs;
            set
            {
                _songs = value;
                OnPropertyChanged(nameof(Songs));
            }
        }
        public ObservableCollection<Playlist> Playlists { get; set; } = new ObservableCollection<Playlist>();
        public Song SelectedSong { get; set; 
        }
        private Playlist _selectedPlaylist;
        public Playlist SelectedPlaylist
        {
            get => _selectedPlaylist;
            set
            {
                if (_selectedPlaylist == value) return;
                _selectedPlaylist = value;
                OnPropertyChanged(nameof(SelectedPlaylist));
            }
        }
        public User CurrentUser { get; set; }

        public ICommand PlayCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand PreviousCommand { get; }
        public ICommand TogglePlayPauseCommand { get; }
        public ICommand AddToFavoritesCommand { get; }
        public ICommand HomeCommand { get; }
        public ICommand ShowFavoriteCommand { get; }

        public MainViewModel()
        {
            PlayCommand = new RelayCommand(o => PlaySelectedSong());
            PauseCommand = new RelayCommand(o => _playerService.Pause());
            NextCommand = new RelayCommand(o => _playerService.PlayNext());
            PreviousCommand = new RelayCommand(o => HandlePrevious());
            TogglePlayPauseCommand = new RelayCommand(o => TogglePlayPause());
            AddToFavoritesCommand = new RelayCommand(o =>
            {
                if (o is Song song && CurrentUser != null)
                {
                    ToggleFavorite(song);
                }
            });
            HomeCommand = new RelayCommand(o => ShowHome());
            ShowFavoriteCommand = new RelayCommand(o => ShowFavoritePlaylist());
        }

        private void ShowFavoritePlaylist()
        {
            if (CurrentUser == null)
            {
                Console.WriteLine("Error: CurrentUser is null.");
                return;
            }

            var favoritePlaylist = Playlists.FirstOrDefault(p => p.Name == "Favorite");
            if (favoritePlaylist == null)
            {
                Console.WriteLine("Favorite playlist not found.");
                return;
            }
            var songsFromDb = _dbService.GetPlaylistSongs(favoritePlaylist.PlaylistId)
                             ?? new ObservableCollection<Song>();

            Application.Current.Dispatcher.Invoke(() =>
            {
                Songs.Clear();
                foreach (var s in songsFromDb)
                    Songs.Add(s);
            });

            SelectedPlaylist = favoritePlaylist;
            Console.WriteLine($"[UI Updated] Favorite playlist reloaded ({Songs.Count} songs).");
        }

        private void ShowHome()
        {
            if (CurrentUser == null)
            {
                Console.WriteLine("Error: CurrentUser is null.");
                return;
            }

            Console.WriteLine("[Home] Loading all songs...");

            var allSongs = _dbService.GetSongs() ?? new ObservableCollection<Song>();

            Application.Current.Dispatcher.Invoke(() =>
            {
                Songs.Clear();
                foreach (var song in allSongs)
                    Songs.Add(song);
            });

            SelectedPlaylist = null;
            Console.WriteLine($"[Home] Loaded {Songs.Count} songs (all library).");
        }

        private void ToggleFavorite(Song song)
        {
            if (song == null || CurrentUser == null)
            {
                Console.WriteLine("Error: Song or CurrentUser is null.");
                return;
            }

            var favoritePlaylist = Playlists.FirstOrDefault(p => p.Name == "Favorite");
            if (favoritePlaylist == null)
            {
                Console.WriteLine("Favorite playlist not found, creating new one.");
                using (var conn = new SqlConnection(_dbService._connectionString))
                {
                    conn.Open();
                    string insertQuery = @"
                INSERT INTO Playlists (Name, UserId, CreatedDate) 
                VALUES (@Name, @UserId, GETDATE());
                SELECT SCOPE_IDENTITY();";
                    using (var cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", "Favorite");
                        cmd.Parameters.AddWithValue("@UserId", CurrentUser.UserId);
                        var playlistId = Convert.ToInt32(cmd.ExecuteScalar());
                        favoritePlaylist = new Playlist
                        {
                            PlaylistId = playlistId,
                            Name = "Favorite",
                            UserId = CurrentUser.UserId,
                            CreatedDate = DateTime.Now
                        };
                        Playlists.Add(favoritePlaylist);
                    }
                }
            }

            var favoriteSongs = _dbService.GetPlaylistSongs(favoritePlaylist.PlaylistId);

            bool isAlreadyFavorite = favoriteSongs.Any(s => s.SongId == song.SongId);

            using (var conn = new SqlConnection(_dbService._connectionString))
            {
                conn.Open();

                if (isAlreadyFavorite)
                {
                    string deleteQuery = "DELETE FROM PlaylistSongs WHERE PlaylistId = @PlaylistId AND SongId = @SongId";
                    using (var cmd = new SqlCommand(deleteQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@PlaylistId", favoritePlaylist.PlaylistId);
                        cmd.Parameters.AddWithValue("@SongId", song.SongId);
                        cmd.ExecuteNonQuery();
                    }
                    Console.WriteLine($"Removed song {song.Title} from Favorites");
                }
                else
                {
                    string insertQuery = @"
                INSERT INTO PlaylistSongs (PlaylistId, SongId, Position)
                VALUES (@PlaylistId, @SongId, 
                        (SELECT COALESCE(MAX(CAST(Position AS INT)), 0) + 1 
                         FROM PlaylistSongs WHERE PlaylistId = @PlaylistId))";
                    using (var cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@PlaylistId", favoritePlaylist.PlaylistId);
                        cmd.Parameters.AddWithValue("@SongId", song.SongId);
                        cmd.ExecuteNonQuery();
                    }
                    Console.WriteLine($"Added song {song.Title} to Favorites");
                }
            }

            // 🔄 Обновляем UI, если пользователь сейчас в "Favorite"
            if (SelectedPlaylist != null && SelectedPlaylist.Name == "Favorite")
            {
                var updatedSongs = _dbService.GetPlaylistSongs(favoritePlaylist.PlaylistId);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Songs.Clear();
                    foreach (var s in updatedSongs)
                        Songs.Add(s);
                });
            }
            song.IsFavorite = !isAlreadyFavorite;
        }

        public void LoadPlaylistSongs(Playlist playlist)
        {
            if (playlist != null && CurrentUser != null)
            {
                SelectedPlaylist = playlist;
                Songs = _dbService.GetPlaylistSongs(playlist.PlaylistId) ?? new ObservableCollection<Song>();
                OnPropertyChanged(nameof(Songs));
            }
        }

        public bool Register(string username, string password, string email)
        {
            bool success = _dbService.RegisterUser(username, password, email);
            if (success)
            {
                CurrentUser = _dbService.Authenticate(username, password);
                if (CurrentUser != null)
                {
                    Songs = _dbService.GetSongs();
                    Playlists = _dbService.GetPlaylists(CurrentUser.UserId);
                    OnPropertyChanged(nameof(Songs));
                    OnPropertyChanged(nameof(Playlists));
                }
            }
            return success;
        }

        public void Login(string username, string password)
        {
            CurrentUser = _dbService.Authenticate(username, password);
            if (CurrentUser != null)
            {
                Songs = _dbService.GetSongs();
                Playlists = _dbService.GetPlaylists(CurrentUser.UserId);
                OnPropertyChanged(nameof(Songs));
                OnPropertyChanged(nameof(Playlists));

                AddSongsFromDirectory(DefaultMusicPath);
            }
        }

        public bool ResetPassword(string email, string newPassword)
        {
            bool success = _dbService.ResetPassword(email, newPassword);
            return success;
        }

        private void PlaySelectedSong()
        {
            if (SelectedSong != null)
            {
                if (!System.IO.File.Exists(SelectedSong.FilePath))
                {
                    return;
                }
                var playlistToUse = SelectedPlaylist != null ? _dbService.GetPlaylistSongs(SelectedPlaylist.PlaylistId) : Songs;
                int songIndex = playlistToUse.ToList().FindIndex(s => s.SongId == SelectedSong.SongId);
                _playerService.Play(SelectedSong, playlistToUse, songIndex);
            }
        }

        private void TogglePlayPause()
        {
            if (_playerService.IsPlaying)
            {
                _playerService.Pause();
            }
            else if (_playerService.CurrentSong != null && _playerService.IsPaused)
            {
                _playerService.Resume(); // Продолжаем с текущей позиции
            }
            else if (SelectedSong != null)
            {
                var currentOrSelected = _playerService.CurrentSong ?? SelectedSong;
                if (!System.IO.File.Exists(currentOrSelected.FilePath))
                {
                    return;
                }
                var playlistToUse = SelectedPlaylist != null ? _dbService.GetPlaylistSongs(SelectedPlaylist.PlaylistId) : Songs;
                int songIndex = playlistToUse.ToList().FindIndex(s => s.SongId == currentOrSelected.SongId);
                _playerService.Play(currentOrSelected, playlistToUse, songIndex);
            }
        }

        private void HandlePrevious()
        {
            if (_playerService.PositionInSeconds < 10 && _playerService.CurrentIndex > 0)
            {
                // Переключение на предыдущий трек, если позиция < 10 сек
                _playerService.PlayPrevious();
            }
            else if (_playerService.CurrentSong != null)
            {
                // Перезапуск текущего трека, если позиция >= 10 сек
                _playerService.PositionInSeconds = 0;
                _playerService.Play(_playerService.CurrentSong, _playerService.Playlist, _playerService.CurrentIndex);
            }
        }

        public void AddSongToCollection(Song song)
        {
            if (Songs == null) Songs = new ObservableCollection<Song>();
            Songs.Add(song);
        }

        public void SearchSongs(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                Songs = SelectedPlaylist != null ? _dbService.GetPlaylistSongs(SelectedPlaylist.PlaylistId) : _dbService.GetSongs();
            }
            else
            {
                var allSongs = _dbService.GetSongs();
                Songs = new ObservableCollection<Song>(allSongs.Where(s =>
                    s.Title.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >=0 ||
                    s.Artist.Name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >=0 ||
                    s.Genre.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >=0));
            }
            OnPropertyChanged(nameof(Songs));
        }

        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void AddSongsFromDirectory(string directoryPath = DefaultMusicPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                return;
            }

            var musicFiles = Directory.GetFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => f.EndsWith(".mp3") || f.EndsWith(".wav"));  // Поддержка MP3/WAV

            foreach (var file in musicFiles)
            {
                if (_dbService.SongExists(file)) continue;  // Пропустить, если уже в БД

                try
                {
                    using (var tagFile = TagLib.File.Create(file))  // TagLib.File
                    {
                        var tags = tagFile.Tag;
                        var artistName = tags.Performers.FirstOrDefault() ?? "Unknown Artist";
                        var albumTitle = tags.Album ?? "Without Album";

                        int artistId = _dbService.AddOrGetArtist(artistName);
                        int albumId = _dbService.AddOrGetAlbum(albumTitle, artistId, (int)(tags.Year > 0 ? tags.Year : 0));

                        string coverPath = null;
                        if (tagFile.Tag.Pictures.Length > 0)
                        {
                            var picture = tagFile.Tag.Pictures[0];
                            coverPath = Path.Combine(Path.GetDirectoryName(file), $"{Path.GetFileNameWithoutExtension(file)}_cover.jpg");
                            System.IO.File.WriteAllBytes(coverPath, picture.Data.Data);
                        }

                        var song = new Song
                        {
                            Title = tags.Title ?? Path.GetFileNameWithoutExtension(file),
                            ArtistId = artistId,
                            AlbumId = albumId,
                            FilePath = file,
                            Duration = tagFile.Properties.Duration,
                            Genre = tags.Genres.FirstOrDefault() ?? "Unknown"
                        };
                        song.CoverImage = _dbService.GetCoverImage(file);
                        _dbService.AddSong(song);
                        Songs.Add(song);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при обработке {file}: {ex.Message}");
                }
            }
            OnPropertyChanged(nameof(Songs));
        }

        // В XAML биндим на PlayerService.IsPlaying, так как сервис доступен через ViewModel
        public MediaPlayerService PlayerService => _playerService;


        public class RelayCommand : ICommand
        {
            private readonly Action<object> _execute;
            private readonly Func<object, bool> _canExecute;

            public event EventHandler CanExecuteChanged
            {
                add => CommandManager.RequerySuggested += value;
                remove => CommandManager.RequerySuggested -= value;
            }

            public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
            {
                _execute = execute;
                _canExecute = canExecute;
            }

            public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
            public void Execute(object parameter) => _execute(parameter);
        }
    }
}
