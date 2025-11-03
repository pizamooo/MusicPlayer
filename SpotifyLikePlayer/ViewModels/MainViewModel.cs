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
        public ObservableCollection<Song> AllSongs { get; set; } = new ObservableCollection<Song>();

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
        public Song SelectedSong { get; set; }

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
        public ICommand CreatePlaylistCommand { get; }
        public ICommand AddToPlaylistCommand { get; }
        public ICommand DeletePlaylistCommand { get; set; }
        public ICommand RemoveFromPlaylistCommand { get; }
        public ObservableCollection<Playlist> AvailablePlaylists => Playlists;


        public MainViewModel()
        {
            PlayCommand = new RelayCommand(o => PlaySelectedSong());
            PauseCommand = new RelayCommand(o => _playerService.Pause());
            NextCommand = new RelayCommand(o => _playerService.PlayNext());
            PreviousCommand = new RelayCommand(o => HandlePrevious());
            TogglePlayPauseCommand = new RelayCommand(o => TogglePlayPause());
            _playerService.PropertyChanged += PlayerService_PropertyChanged;
            _dbService = new DatabaseService();
            Playlists = new ObservableCollection<Playlist>(_dbService.GetPlaylists().OrderBy(p => p.Name != "Favorite").ThenBy(p => p.Name));
            CreatePlaylistCommand = new RelayCommand(_ => CreatePlaylistDialog());
            RemoveFromPlaylistCommand = new RelayCommand(RemoveFromPlaylist);
            DeletePlaylistCommand = new RelayCommand(o =>
            {
                if (o is Playlist playlist)
                    DeletePlaylist(playlist);
            });
            AddToPlaylistCommand = new RelayCommand(AddToPlaylist);
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

        public void LoadSongsByGenre(string genre)
        {
            try
            {
                Songs.Clear();

                var filteredSongs = _dbService.GetSongsByGenre(genre);

                foreach (var song in filteredSongs)
                    Songs.Add(song);
                UpdateFavoriteFlags();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при загрузке песен по жанру {genre}: {ex.Message}");
            }
        }

        private void Notify(string message, bool isPositive = true)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var main = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                main?.ShowNotification(message, isPositive);
            });
        }

        public void RemoveFromPlaylist(object parameter)
        {
            Song song = null;
            Playlist playlist = null;

            // Проверяем тип параметра (Tuple<Playlist, Song> или Song)
            if (parameter is Tuple<Playlist, Song> tuple)
            {
                playlist = tuple.Item1;
                song = tuple.Item2;
            }
            else if (parameter is Song singleSong)
            {
                song = singleSong;
            }

            if (song == null)
            {
                Notify("Ошибка удаления: песня не выбрана.", false);
                return;
            }

            try
            {
                using (var conn = new SqlConnection(_dbService._connectionString))
                {
                    conn.Open();

                    if (playlist != null)
                    {
                        // Удаление из одного конкретного плейлиста
                        using (var cmd = new SqlCommand("DELETE FROM PlaylistSongs WHERE PlaylistId=@PlaylistId AND SongId=@SongId", conn))
                        {
                            cmd.Parameters.AddWithValue("@PlaylistId", playlist.PlaylistId);
                            cmd.Parameters.AddWithValue("@SongId", song.SongId);
                            int rows = cmd.ExecuteNonQuery();

                            if (rows > 0)
                                Notify($"Песня удалена из «{playlist.Name}».", true);
                            else
                                Notify($"Песни не было в «{playlist.Name}».", false);
                        }
                    }
                    else
                    {
                        // Удаляем из всех плейлистов, где песня присутствует
                        using (var cmd = new SqlCommand("DELETE FROM PlaylistSongs WHERE SongId=@SongId", conn))
                        {
                            cmd.Parameters.AddWithValue("@SongId", song.SongId);
                            int rows = cmd.ExecuteNonQuery();

                            if (rows > 0)
                                Notify($"Песня удалена из всех плейлистов.", true);
                            else
                                Notify($"Песня не найдена ни в одном плейлисте.", false);
                        }
                    }
                }

                // Обновляем список песен в текущем активном плейлисте, если нужно
                if (SelectedPlaylist != null)
                {
                    LoadPlaylistSongs(SelectedPlaylist);
                }
            }
            catch (Exception ex)
            {
                Notify($"Ошибка при удалении: {ex.Message}", false);
            }
        }
        public void AddToPlaylist(object parameter)
        {
            if (!(parameter is Tuple<Playlist, Song> tuple)) return;
            var playlist = tuple.Item1;
            var song = tuple.Item2;

            var existing = _dbService.GetPlaylistSongs(playlist.PlaylistId)?.Any(s => s.SongId == song.SongId) ?? false;
            if (existing)
            {
                Notify($"Песня уже есть в \"{playlist.Name}\"", false);
                return;
            }

            _dbService.AddSongToPlaylist(playlist.PlaylistId, song.SongId);
            Notify($"Песня добавлена в \"{playlist.Name}\"", true);

            if (SelectedPlaylist != null && SelectedPlaylist.PlaylistId == playlist.PlaylistId)
                LoadPlaylistSongs(SelectedPlaylist);
        }

        private void CreatePlaylistDialog()
        {
            if (CurrentUser == null)
            {
                Notify("Нужно войти в аккаунт, чтобы создать плейлист.", false);
                return;
            }

            if (Playlists != null && Playlists.Count >= 13)
            {
                Notify("Нельзя добавить песню — достигнут лимит плейлистов!", false);
                return;
            }

            string name = Microsoft.VisualBasic.Interaction.InputBox(
                "Введите имя нового плейлиста:", "Создание плейлиста", "Новый плейлист");

            if (string.IsNullOrWhiteSpace(name)) return;

            var newPlaylist = _dbService.CreatePlaylist(name, CurrentUser.UserId);
            if (newPlaylist != null)
            {
                Playlists.Add(newPlaylist);
                OnPropertyChanged(nameof(Playlists));
                Notify($"Плейлист \"{name}\" успешно создан!", true);
            }
            else
            {
                Notify("Ошибка при создании плейлиста.", false);
            }
        }

        private void DeletePlaylist(Playlist playlist)
        {
            if (playlist == null)
                return;

            if (playlist.Name.Equals("Favorite", StringComparison.OrdinalIgnoreCase))
            {
                Notify("Нельзя удалить плейлист 'Favorite'.", false);
                return;
            }

            try
            {
                using (var conn = new SqlConnection(_dbService._connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("DELETE FROM PlaylistSongs WHERE PlaylistId = @PlaylistId", conn))
                    {
                        cmd.Parameters.AddWithValue("@PlaylistId", playlist.PlaylistId);
                        cmd.ExecuteNonQuery();
                    }
                    using (var cmd = new SqlCommand("DELETE FROM Playlists WHERE PlaylistId = @PlaylistId", conn))
                    {
                        cmd.Parameters.AddWithValue("@PlaylistId", playlist.PlaylistId);
                        cmd.ExecuteNonQuery();
                    }
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Playlists.Remove(playlist);
                    OnPropertyChanged(nameof(Playlists));
                });

                ShowHome();
                Notify($"Плейлист \"{playlist.Name}\" удалён.", true);
            }
            catch (Exception ex)
            {
                Notify($"Ошибка при удалении: {ex.Message}", false);
            }
        }

        public void CreatePlaylistFromSong(Song song)
        {
            var name = Microsoft.VisualBasic.Interaction.InputBox(
                "Введите имя нового плейлиста:", "Создать плейлист", "Новый плейлист");

            if (string.IsNullOrWhiteSpace(name)) return;
            if (CurrentUser == null)
            {
                (Application.Current.MainWindow as MainWindow)?.ShowNotification("Нужно войти в аккаунт, чтобы создать плейлист", false);
                return;
            }

            var newPlaylist = _dbService.CreatePlaylist(name, CurrentUser.UserId);
            if (newPlaylist != null)
            {
                Playlists.Add(newPlaylist);
                Notify($"Плейлист «{newPlaylist.Name}» создан", true);
                if (song != null)
                {
                    _dbService.AddSongToPlaylist(newPlaylist.PlaylistId, song.SongId);
                    Notify($"Песня добавлена в «{newPlaylist.Name}»", true);
                }
            }
        }

        private void CreatePlaylist(object parameter)
        {
            string name = Microsoft.VisualBasic.Interaction.InputBox(
                "Введите имя нового плейлиста:", "Создание плейлиста", "Новый плейлист");

            if (string.IsNullOrWhiteSpace(name))
                return;

            var newPlaylist = _dbService.CreatePlaylist(name, CurrentUser?.UserId ?? 0);

            if (newPlaylist != null)
            {
                Playlists.Add(newPlaylist);
                OnPropertyChanged(nameof(Playlists));
            }
        }

        public void ShowAddToPlaylistDialogAndAdd(Song song)
        {
            // Простейшая реализация: спросим имя плейлиста через InputBox
            string playlistNames = string.Join("\n", Playlists.Select(p => p.Name));
            var chosen = Microsoft.VisualBasic.Interaction.InputBox($"Введите имя плейлиста:\n\n{playlistNames}", "Добавить в плейлист");
            if (string.IsNullOrWhiteSpace(chosen)) return;

            var playlist = Playlists.FirstOrDefault(p => p.Name.Equals(chosen, StringComparison.OrdinalIgnoreCase));
            if (playlist == null)
            {
                Notify("Плейлист не найден", false);
                return;
            }

            _dbService.AddSongToPlaylist(playlist.PlaylistId, song.SongId);
            Notify($"Песня добавлена в «{playlist.Name}»", true);

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

        public void SyncSelectedSongWithCurrent()
        {
            if (PlayerService.CurrentSong != null && Songs != null)
            {
                SelectedSong = Songs.FirstOrDefault(s => s.SongId == PlayerService.CurrentSong.SongId);
                OnPropertyChanged(nameof(SelectedSong));

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var main = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                    main?.SongsListView?.ScrollIntoView(SelectedSong);
                    main?.SongsListView?.UpdateLayout();

                    foreach (var s in Songs)
                    {
                        s.IsPlaying = (s.SongId == PlayerService.CurrentSong.SongId);
                    }
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

            SyncSelectedSongWithCurrent();
        }

        public void UpdateFavoriteFlags()
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
                AllSongs.Clear();
                Songs.Clear();

                foreach (var song in allSongs)
                {
                    AllSongs.Add(song);
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
                return;

            song.IsFavoriteLocal = !song.IsFavoriteLocal;

            var favoritePlaylist = Playlists.FirstOrDefault(p => p.Name.Equals("Favorite", StringComparison.OrdinalIgnoreCase));
            if (favoritePlaylist == null)
            {
                using (var conn = new SqlConnection(_dbService._connectionString))
                {
                    conn.Open();
                    string query = @"
                INSERT INTO Playlists (Name, UserId, CreatedDate)
                VALUES (@Name, @UserId, GETDATE());
                SELECT SCOPE_IDENTITY();";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Name", "Favorite");
                        cmd.Parameters.AddWithValue("@UserId", CurrentUser.UserId);
                        int id = Convert.ToInt32(cmd.ExecuteScalar());
                        favoritePlaylist = new Playlist
                        {
                            PlaylistId = id,
                            Name = "Favorite",
                            UserId = CurrentUser.UserId,
                            CreatedDate = DateTime.Now
                        };
                        Playlists.Add(favoritePlaylist);
                    }
                }
            }

            bool inDb;
            using (var conn = new SqlConnection(_dbService._connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand("SELECT COUNT(*) FROM PlaylistSongs WHERE PlaylistId=@pl AND SongId=@s", conn);
                cmd.Parameters.AddWithValue("@pl", favoritePlaylist.PlaylistId);
                cmd.Parameters.AddWithValue("@s", song.SongId);
                inDb = (int)cmd.ExecuteScalar() > 0;
            }

            if (song.IsFavoriteLocal && !inDb)
            {
                using (var conn = new SqlConnection(_dbService._connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                INSERT INTO PlaylistSongs (PlaylistId, SongId, Position)
                VALUES (@p, @s, (SELECT COALESCE(MAX(Position), 0) + 1 FROM PlaylistSongs WHERE PlaylistId = @p))", conn);
                    cmd.Parameters.AddWithValue("@p", favoritePlaylist.PlaylistId);
                    cmd.Parameters.AddWithValue("@s", song.SongId);
                    cmd.ExecuteNonQuery();
                }
                song.IsFavorite = true;
            }
            else if (!song.IsFavoriteLocal && inDb)
            {
                using (var conn = new SqlConnection(_dbService._connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("DELETE FROM PlaylistSongs WHERE PlaylistId=@p AND SongId=@s", conn);
                    cmd.Parameters.AddWithValue("@p", favoritePlaylist.PlaylistId);
                    cmd.Parameters.AddWithValue("@s", song.SongId);
                    cmd.ExecuteNonQuery();
                }
                song.IsFavorite = false;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var s in Songs.Where(x => x.SongId == song.SongId))
                {
                    s.IsFavorite = song.IsFavoriteLocal;
                    s.IsFavoriteLocal = song.IsFavoriteLocal;
                }

                foreach (var pl in Playlists)
                {
                    foreach (var s in pl.Songs.Where(x => x.SongId == song.SongId))
                    {
                        s.IsFavorite = song.IsFavoriteLocal;
                        s.IsFavoriteLocal = song.IsFavoriteLocal;
                    }
                }

                if (CurrentSong != null && song.SongId == CurrentSong.SongId)
                {
                    CurrentSong.IsFavorite = song.IsFavoriteLocal;
                    CurrentSong.IsFavoriteLocal = song.IsFavoriteLocal;
                }

                RefreshSongInView(song);
                OnPropertyChanged(nameof(Songs));
            });

            if (SelectedPlaylist?.Name == "Favorite")
                UpdateFavoritePlaylist();

            Application.Current.Dispatcher.Invoke(() =>
            {
                var main = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                main?.ShowNotification(
                    song.IsFavoriteLocal
                        ? $"\"{song.Title}\" добавлена в избранное"
                        : $"\"{song.Title}\" удалена из избранного",
                    song.IsFavoriteLocal
                );
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
            if (playlist == null) return;

            var songs = _dbService.GetPlaylistSongs(playlist.PlaylistId);

            // Загружаем список избранных песен пользователя
            var favoritePlaylist = Playlists.FirstOrDefault(p => p.Name == "Favorite");
            var favoriteSongs = favoritePlaylist != null
                ? _dbService.GetPlaylistSongs(favoritePlaylist.PlaylistId).Select(s => s.SongId).ToList()
                : new List<int>();

            Application.Current.Dispatcher.Invoke(() =>
            {
                Songs.Clear();
                foreach (var s in songs)
                {
                    s.IsFavorite = favoriteSongs.Contains(s.SongId);
                    Songs.Add(s);
                }

                SelectedPlaylist = playlist;
                OnPropertyChanged(nameof(Songs));

                SyncSelectedSongWithCurrent();
            });
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
                var allSongs = _dbService.GetSongs();
                AllSongs = new ObservableCollection<Song>(allSongs);
                Songs = new ObservableCollection<Song>(allSongs);
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

        public void PlaySelectedSong()
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

        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public async Task AddSongsFromDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                return;

            var files = Directory.GetFiles(directoryPath, "*.mp3", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                try
                {
                    // Пропускаем, если трек уже есть
                    if (_dbService.SongExists(file))
                        continue;

                    using (var tagFile = TagLib.File.Create(file))
                    {
                        // Заголовок
                        string title = !string.IsNullOrWhiteSpace(tagFile.Tag.Title)
                            ? tagFile.Tag.Title
                            : Path.GetFileNameWithoutExtension(file);

                        // Исполнитель
                        string artistName = tagFile.Tag.FirstPerformer ?? "Unknown Artist";
                        int artistId = _dbService.AddOrGetArtist(artistName);

                        // Альбом
                        string albumTitle = tagFile.Tag.Album ?? "Single";
                        int albumId = _dbService.AddOrGetAlbum(albumTitle, artistId);

                        // Жанр
                        string genre = tagFile.Tag.FirstGenre ?? "Unknown";

                        // Длительность (в секундах)
                        TimeSpan duration = tagFile.Properties.Duration;

                        // Создаём объект песни
                        var song = new Song
                        {
                            Title = title,
                            ArtistId = artistId,
                            AlbumId = albumId,
                            Genre = genre,
                            FilePath = file,
                            Duration = TimeSpan.FromSeconds(Math.Round(tagFile.Properties.Duration.TotalSeconds, 0)),
                            Artist = new Artist { ArtistId = artistId, Name = artistName },
                            Album = new Album { AlbumId = albumId, Title = albumTitle },
                            CoverImage = _dbService.GetCoverImage(file)
                        };

                        // Добавляем в БД
                        _dbService.AddSong(song);

                        // Добавляем в коллекцию UI
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            Songs.Add(song);
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при обработке файла {file}: {ex.Message}");
                }
            }

            // Обновляем UI
            OnPropertyChanged(nameof(Songs));
        }

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
