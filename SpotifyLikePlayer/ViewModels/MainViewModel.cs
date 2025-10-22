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

        private Song _currentSong;
        public Song CurrentSong
        {
            get => _currentSong;
            set
            {
                if (_currentSong != value)
                {
                    _currentSong = value;
                    OnPropertyChanged(nameof(CurrentSong));
                }
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private Song _lastObservedSong;

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
        public ICommand ToggleFavoriteCommand { get; }


        public MainViewModel()
        {
            PlayCommand = new RelayCommand(o => PlaySelectedSong());
            PauseCommand = new RelayCommand(o => _playerService.Pause());
            NextCommand = new RelayCommand(o => _playerService.PlayNext());
            PreviousCommand = new RelayCommand(o => HandlePrevious());
            TogglePlayPauseCommand = new RelayCommand(o => TogglePlayPause());
            _playerService.PropertyChanged += PlayerService_PropertyChanged;
            ToggleFavoriteCommand = new RelayCommand(o =>
            {
                if (o is Song song)
                    ToggleFavorite(song);
            });
            AddToFavoritesCommand = new RelayCommand(o =>
            {
                if (o is Song song && CurrentUser != null)
                {
                    ToggleFavorite(song);
                }
            });
            HomeCommand = new RelayCommand(o => ShowHome());
            ShowFavoriteCommand = new RelayCommand(o => ShowFavoritePlaylist());
            _playerService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MediaPlayerService.CurrentSong))
                    SyncSelectedSongWithCurrent();
            };
            _playerService.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(MediaPlayerService.CurrentSong))
                {
                    SyncSelectedSongWithCurrent();

                    if (_playerService.CurrentSong != null)
                    {
                        // Отписываемся, если уже слушали старую песню (во избежание утечек)
                        _playerService.CurrentSong.PropertyChanged -= CurrentSong_PropertyChanged;
                        _playerService.CurrentSong.PropertyChanged += CurrentSong_PropertyChanged;
                    }
                }
            };
        }

        private void PlayerService_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MediaPlayerService.CurrentSong))
            {
                // синхронизируем SelectedSong с CurrentSong
                SyncSelectedSongWithCurrent();

                // отписываемся от предыдущей песни (если была)
                if (_lastObservedSong != null)
                {
                    _lastObservedSong.PropertyChanged -= CurrentSong_PropertyChanged;
                    _lastObservedSong = null;
                }

                // подписываемся на PropertyChanged новой текущей песни
                var current = _playerService.CurrentSong;
                if (current != null)
                {
                    current.PropertyChanged += CurrentSong_PropertyChanged;
                    _lastObservedSong = current;
                }
            }
        }

        private void CurrentSong_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Song.IsFavoriteLocal))
            {
                // безопасно возьмём текущую песню
                var current = _playerService.CurrentSong;
                if (current == null) return;

                // Обновляем соответствующий элемент в Songs и UI в UI-потоке
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var songInList = Songs.FirstOrDefault(s => s.SongId == current.SongId);
                    if (songInList != null)
                    {
                        songInList.IsFavoriteLocal = current.IsFavoriteLocal;
                        RefreshSongInView(songInList);
                    }
                });
            }
        }

        private void SyncSelectedSongWithCurrent()
        {
            if (PlayerService.CurrentSong != null && Songs != null)
            {
                SelectedSong = Songs.FirstOrDefault(s => s.SongId == PlayerService.CurrentSong.SongId);
                OnPropertyChanged(nameof(SelectedSong));

                // Автоматически скроллим ListView к текущему треку
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var main = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                    main?.SongsListView?.ScrollIntoView(SelectedSong);
                    main?.SongsListView?.UpdateLayout();
                });
            }
        }

        private void RefreshSongInView(Song song)
        {
            if (song == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    int index = Songs.IndexOf(song);
                    if (index < 0) return;

                    Songs.RemoveAt(index);
                    Songs.Insert(index, song);

                    Console.WriteLine($"RefreshSongInView: refreshed song at index {index}, id={song.SongId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"RefreshSongInView error: {ex}");
                }
            });
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

            SelectedPlaylist = favoritePlaylist;
            UpdateFavoritePlaylist();
            Console.WriteLine($"Loaded {Songs.Count} songs from Favorite playlist.");
        }

        private void UpdateFavoriteFlags()
        {
            var favoritePlaylist = Playlists.FirstOrDefault(p => p.Name == "Favorite");
            var favoriteIds = favoritePlaylist != null
                ? _dbService.GetPlaylistSongs(favoritePlaylist.PlaylistId)?.Select(s => s.SongId).ToHashSet()
                : new HashSet<int>();

            foreach (var song in Songs.ToList())
            {
                bool previousFavorite = song.IsFavorite;
                song.IsFavorite = favoriteIds.Contains(song.SongId);
                if (previousFavorite != song.IsFavorite)
                {
                    Console.WriteLine($"Updated IsFavorite for song ID {song.SongId} to {song.IsFavorite}");
                    song.IsFavoriteLocal = song.IsFavorite; // Синхронизация для "Home"
                }
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(Songs));
            });
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
                {
                    Songs.Add(song);
                }
            });

            UpdateFavoriteFlags();
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

            song.IsFavoriteLocal = !song.IsFavoriteLocal;

            var favoritePlaylist = Playlists.FirstOrDefault(p => p.Name == "Favorite");
            if (favoritePlaylist == null)
            {
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

            var existingSongs = _dbService.GetPlaylistSongs(favoritePlaylist.PlaylistId);
            bool isCurrentlyFavorite = existingSongs.Any(s => s.SongId == song.SongId);

            if (!isCurrentlyFavorite && song.IsFavoriteLocal)
            {
                using (var conn = new SqlConnection(_dbService._connectionString))
                {
                    conn.Open();
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
                }
                song.IsFavorite = true;
            }
            else if (isCurrentlyFavorite && !song.IsFavoriteLocal)
            {
                using (var conn = new SqlConnection(_dbService._connectionString))
                {
                    conn.Open();
                    string deleteQuery = "DELETE FROM PlaylistSongs WHERE PlaylistId = @PlaylistId AND SongId = @SongId";
                    using (var cmd = new SqlCommand(deleteQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@PlaylistId", favoritePlaylist.PlaylistId);
                        cmd.Parameters.AddWithValue("@SongId", song.SongId);
                        cmd.ExecuteNonQuery();
                    }
                }
                song.IsFavorite = false;
            }
            if (SelectedPlaylist?.Name == "Favorite")
                UpdateFavoritePlaylist();
            else
                RefreshSongInView(song);
            if (CurrentSong != null && song.SongId == CurrentSong.SongId)
            {
                OnPropertyChanged(nameof(CurrentSong));
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                var main = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                main?.ShowFavoriteNotification(
                    song.IsFavoriteLocal ? "⭐ Added to favorites" : "❌ Removed from favorites",
                    song.IsFavoriteLocal);
            });
        }
        private void UpdateFavoritePlaylist()
        {
            var favoritePlaylist = Playlists.FirstOrDefault(p => p.Name == "Favorite");
            if (favoritePlaylist == null) return;

            var updatedSongsFromDb = _dbService.GetPlaylistSongs(favoritePlaylist.PlaylistId) ?? new ObservableCollection<Song>();
            Application.Current.Dispatcher.Invoke(() =>
            {
                var songDict = Songs.ToDictionary(s => s.SongId, s => s);
                Songs.Clear();
                foreach (var newSong in updatedSongsFromDb)
                {
                    if (songDict.TryGetValue(newSong.SongId, out var existingSong))
                    {
                        existingSong.IsFavorite = true;
                        existingSong.IsFavoriteLocal = true;
                        Songs.Add(existingSong);
                    }
                    else
                    {
                        newSong.IsFavorite = true;
                        newSong.IsFavoriteLocal = true;
                        Songs.Add(newSong);
                    }
                }
                OnPropertyChanged(nameof(Songs));
            });
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
                    UpdateFavoriteFlags();
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
                UpdateFavoriteFlags();
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
