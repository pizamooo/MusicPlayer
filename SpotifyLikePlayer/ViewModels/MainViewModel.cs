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

namespace SpotifyLikePlayer.ViewModels
{
    public class MainViewModel
    {
        public DatabaseService _dbService = new DatabaseService();
        public MediaPlayerService _playerService = new MediaPlayerService();
        private const string DefaultMusicPath = @"C:\Users\dobry\OneDrive\Documents\MusicForProject";  // путь песен

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<Song> Songs { get; set; } = new ObservableCollection<Song>();
        public ObservableCollection<Playlist> Playlists { get; set; } = new ObservableCollection<Playlist>();
        public ObservableCollection<Song> DisplayedSongs { get; set; } = new ObservableCollection<Song>(); // Новая коллекция для отображения
        public Song SelectedSong { get; set; }
        public Playlist SelectedPlaylist { get; set; }
        public User CurrentUser { get; set; } = new User { UserId = 1 };
        private Playlist _favoritePlaylist;

        public ICommand PlayCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand NextCommand { get; }
        public ICommand PreviousCommand { get; }
        public ICommand TogglePlayPauseCommand { get; }
        public ICommand AddToFavoriteCommand { get; }
        public ICommand HomeCommand { get; }

        public MainViewModel()
        {
            PlayCommand = new RelayCommand(o => PlaySelectedSong());
            PauseCommand = new RelayCommand(o => _playerService.Pause());
            NextCommand = new RelayCommand(o => _playerService.PlayNext());
            PreviousCommand = new RelayCommand(o => HandlePrevious());
            TogglePlayPauseCommand = new RelayCommand(o => TogglePlayPause());
            AddToFavoriteCommand = new RelayCommand(o => AddToFavorite(), o => SelectedSong != null);
            HomeCommand = new RelayCommand(o => ShowHome());
            LoadInitialData();
        }

        private void LoadInitialData()
        {
            if (CurrentUser == null)
            {
                CurrentUser = new User { UserId = 1 }; // Дефолтный пользователь, если не задан
            }
            try
            {
                Playlists = _dbService.GetPlaylists(CurrentUser.UserId) ?? new ObservableCollection<Playlist>();
                Songs = _dbService.GetSongs() ?? new ObservableCollection<Song>();
                DisplayedSongs = Songs; // По умолчанию показываем все песни
                OnPropertyChanged(nameof(Playlists));
                OnPropertyChanged(nameof(Songs));
                OnPropertyChanged(nameof(DisplayedSongs));
            }
            catch (Exception ex)
            {
                // Логирование ошибки (например, Console.WriteLine(ex.Message))
                Playlists = new ObservableCollection<Playlist>();
                Songs = new ObservableCollection<Song>();
                DisplayedSongs = new ObservableCollection<Song>();
                OnPropertyChanged(nameof(Playlists));
                OnPropertyChanged(nameof(Songs));
                OnPropertyChanged(nameof(DisplayedSongs));
            }
        }

        private void AddToFavorite()
        {
            if (SelectedSong != null)
            {
                if (_favoritePlaylist == null)
                {
                    _favoritePlaylist = _dbService.GetOrCreateFavoritePlaylist(CurrentUser.UserId);
                }
                if (_favoritePlaylist.Songs.Any(s => s.SongId == SelectedSong.SongId)) return;

                _dbService.AddSongToPlaylist(_favoritePlaylist.PlaylistId, SelectedSong.SongId);
                _favoritePlaylist.Songs = _dbService.GetPlaylistSongs(_favoritePlaylist.PlaylistId) ?? new ObservableCollection<Song>();
                SelectedSong.IsFavorite = true;
                OnPropertyChanged(nameof(Songs));

                if (SelectedPlaylist != null && SelectedPlaylist.Name == "Любимое")
                {
                    LoadPlaylistSongs(SelectedPlaylist);
                }
            }
        }

        private void ShowHome()
        {
            SelectedPlaylist = null;
            DisplayedSongs = Songs;
            OnPropertyChanged(nameof(DisplayedSongs));
        }

        private void LoadPlaylists()
        {
            Playlists = _dbService.GetPlaylists(CurrentUser.UserId);
            OnPropertyChanged(nameof(Playlists));
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
                var playlistToUse = SelectedPlaylist != null ? _dbService.GetPlaylistSongs(SelectedPlaylist.PlaylistId) ?? new ObservableCollection<Song>() : Songs;
                int songIndex = playlistToUse.IndexOf(SelectedSong);
                if (songIndex < 0 && playlistToUse.Any()) songIndex = 0;
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

        public void LoadPlaylistSongs(Playlist playlist)
        {
            if (playlist != null)
            {
                DisplayedSongs = _dbService.GetPlaylistSongs(playlist.PlaylistId) ?? new ObservableCollection<Song>();
                OnPropertyChanged(nameof(DisplayedSongs));
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

        protected void OnPropertyChanged(string propertyName)
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
            private Action<object> _execute;
            private Func<object, bool> _canExecute;
            public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
            {
                _execute = execute;
                _canExecute = canExecute;
            }
            public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;
            public void Execute(object parameter) => _execute(parameter);
            public event EventHandler CanExecuteChanged { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
        }
    }
}
