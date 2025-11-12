using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Threading;
using SpotifyLikePlayer.Models;
using System.Collections.ObjectModel;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using System.Diagnostics;

namespace SpotifyLikePlayer.Services
{
    public class MediaPlayerService : INotifyPropertyChanged
    {
        private readonly MediaPlayer _mediaPlayer = new MediaPlayer();
        private ObservableCollection<Song> _playlist;
        private int _currentIndex;
        private bool _isPlaying;
        private bool _isPaused;
        private double _positionInSeconds;
        private double _volume = 0.5;
        private readonly Stopwatch _updateStopwatch = new Stopwatch();

        private readonly Random _random = new Random();

        public event Action<Song> SongChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _isDragging;
        public bool IsDragging
        {
            get => _isDragging;
            set
            {
                _isDragging = value;
                OnPropertyChanged(nameof(IsDragging));
            }
        }
        public enum RepeatMode
        {
            None,   // обычный режим
            All,    // повтор плейлиста
            One     // повтор текущего трека
        }
        private RepeatMode _repeatMode = RepeatMode.None;
        private bool _isShuffleEnabled;
        public bool IsShuffleEnabled
        {
            get => _isShuffleEnabled;
            set
            {
                if (_isShuffleEnabled != value)
                {
                    _isShuffleEnabled = value;
                    OnPropertyChanged(nameof(IsShuffleEnabled));
                }
            }
        }

        private Song _currentSong;
        public Song CurrentSong
        {
            get => _currentSong;
            private set
            {
                if (_currentSong != value)
                {
                    _currentSong = value;
                    OnPropertyChanged(nameof(CurrentSong));
                }
            }
        }


        public MediaPlayerService()
        {
            _mediaPlayer.MediaEnded += OnMediaEnded;
            _mediaPlayer.MediaOpened += OnMediaOpened;
            CompositionTarget.Rendering += OnRendering;
            _updateStopwatch.Start();
        }

        private void OnMediaOpened(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(DurationInSeconds));
            OnPropertyChanged(nameof(PositionInSeconds));
        }

        public void PreviewPosition(double seconds)
        {
            _positionInSeconds = seconds;
            OnPropertyChanged(nameof(PositionInSeconds));
            OnPropertyChanged(nameof(DurationInSeconds));
        }

        public void Seek(double seconds)
        {
            if (_mediaPlayer.Source == null) return;

            _positionInSeconds = seconds;
            _mediaPlayer.Position = TimeSpan.FromSeconds(seconds);

            OnPropertyChanged(nameof(PositionInSeconds));
        }

        public void RefreshCurrentSong()
        {
            OnPropertyChanged(nameof(CurrentSong));
        }

        public void UpdatePlaylist(ObservableCollection<Song> newPlaylist)
        {
            _playlist = newPlaylist;
            if (CurrentSong != null)
            {
                _currentIndex = _playlist.ToList().FindIndex(s => s.SongId == CurrentSong.SongId);
                if (_currentIndex < 0) _currentIndex = 0;
            }
            else
            {
                _currentIndex = 0;
            }
        }

        private void OnMediaEnded(object sender, EventArgs e)
        {
            if (_playlist == null || _playlist.Count == 0)
                return;

            switch (_repeatMode)
            {
                case RepeatMode.One:
                    _mediaPlayer.Position = TimeSpan.Zero;
                    _mediaPlayer.Play();
                    break;

                case RepeatMode.All:
                    PlayNextWithShuffleCheck();
                    break;

                case RepeatMode.None:
                default:
                    if (_isShuffleEnabled)
                    {
                        PlayNextWithShuffleCheck();
                    }
                    else if (_currentIndex < _playlist.Count - 1)
                    {
                        _currentIndex++;
                        Play(_playlist[_currentIndex], _playlist, _currentIndex);
                    }
                    else
                    {
                        _isPlaying = false;
                    }
                    break;
            }
        }

        private void PlayNextWithShuffleCheck()
        {
            if (_playlist == null || _playlist.Count == 0)
                return;

            if (_isShuffleEnabled)
            {
                var random = new Random();
                int nextIndex;

                if (_playlist.Count == 1)
                {
                    nextIndex = _currentIndex;
                }
                else
                {
                    do
                    {
                        nextIndex = random.Next(0, _playlist.Count);
                    } while (nextIndex == _currentIndex);
                }

                _currentIndex = nextIndex;
                Play(_playlist[_currentIndex], _playlist, _currentIndex);
            }
            else
            {
                if (_currentIndex < _playlist.Count - 1)
                {
                    _currentIndex++;
                }
                else if (_repeatMode == RepeatMode.All)
                {
                    _currentIndex = 0;
                }
                else
                {
                    _isPlaying = false;
                    return;
                }

                Play(_playlist[_currentIndex], _playlist, _currentIndex);
            }
        }

        private void HandleSongEnded()
        {
            if (_repeatMode == RepeatMode.One)
            {
                Play(CurrentSong, _playlist, _currentIndex);
            }
            else
            {
                PlayNext();
            }
        }


        private void OnSongChanged(Song song)
        {
            SongChanged?.Invoke(song);
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (_mediaPlayer.Source == null || !_isPlaying)
                return;

            if (!_isDragging)
            {
                double pos = _mediaPlayer.Position.TotalSeconds;
                if (Math.Abs(pos - _positionInSeconds) > 0.05)
                {
                    _positionInSeconds = pos;
                    OnPropertyChanged(nameof(PositionInSeconds));
                    OnPropertyChanged(nameof(DurationInSeconds));
                }
            }
        }

        public void Play(Song song, ObservableCollection<Song> playlist, int index)
        {
            _playlist = playlist;
            _currentIndex = index;
            _mediaPlayer.Open(new Uri(song.FilePath));
            _mediaPlayer.Position = TimeSpan.FromSeconds(0);
            _mediaPlayer.Play();
            _isPlaying = true;
            _isPaused = false;
            _positionInSeconds = 0;
            CurrentSong = song;

            foreach (var s in _playlist)
            {
                s.IsPlaying = (s.SongId == song.SongId);
                s.OnPropertyChanged(nameof(IsPlaying));
            }

            OnPropertyChanged(nameof(CurrentSong));
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(IsPaused));
            SongChanged?.Invoke(song);
        }

        public void Pause()
        {
            if (_isPlaying)
            {
                _positionInSeconds = _mediaPlayer.Position.TotalSeconds;
                _mediaPlayer.Pause();
                _isPlaying = false;
                _isPaused = true;
                OnPropertyChanged(nameof(IsPlaying));
                OnPropertyChanged(nameof(IsPaused));
            }
        }

        public void Resume()
        {
            if (_isPaused && _mediaPlayer.Source != null)
            {
                _mediaPlayer.Position = TimeSpan.FromSeconds(_positionInSeconds);
                _mediaPlayer.Play();
                _isPlaying = true;
                _isPaused = false;
                OnPropertyChanged(nameof(IsPlaying));
                OnPropertyChanged(nameof(IsPaused));
            }
        }

        public void PlayNext()
        {
            if (_playlist == null || _playlist.Count == 0)
                return;

            int nextIndex = _currentIndex;

            if (_isShuffleEnabled)
            {
                nextIndex = _random.Next(0, _playlist.Count);
            }
            else
            {
                nextIndex = _currentIndex + 1;

                if (nextIndex >= _playlist.Count)
                {
                    if (_repeatMode == RepeatMode.All)
                        nextIndex = 0;
                    else
                        return;
                }
            }

            if (nextIndex < 0 || nextIndex >= _playlist.Count)
                return;

            _currentIndex = nextIndex;
            Play(_playlist[_currentIndex], _playlist, _currentIndex);
        }

        public void PlayPrevious()
        {
            if (_playlist == null || _playlist.Count == 0)
                return;

            int prevIndex = _currentIndex;

            if (_isShuffleEnabled)
            {
                prevIndex = _random.Next(0, _playlist.Count);
            }
            else
            {
                prevIndex = _currentIndex - 1;

                if (prevIndex < 0)
                {
                    if (_repeatMode == RepeatMode.All)
                        prevIndex = _playlist.Count - 1;
                    else
                        return;
                }
            }

            if (prevIndex < 0 || prevIndex >= _playlist.Count)
                return;

            _currentIndex = prevIndex;
            Play(_playlist[_currentIndex], _playlist, _currentIndex);
        }

        public void ToggleShuffle()
        {
            _isShuffleEnabled = !_isShuffleEnabled;
            OnPropertyChanged(nameof(IsShuffleEnabled));
        }

        public void ToggleRepeatMode()
        {
            switch (_repeatMode)
            {
                case RepeatMode.None:
                    _repeatMode = RepeatMode.All;
                    break;
                case RepeatMode.All:
                    _repeatMode = RepeatMode.One;
                    break;
                case RepeatMode.One:
                default:
                    _repeatMode = RepeatMode.None;
                    break;
            }

            OnPropertyChanged(nameof(RepeatModeState));
        }

        public bool IsPlaying => _isPlaying;
        public bool IsPaused => _isPaused;
        public RepeatMode RepeatModeState
        {
            get => _repeatMode;
            set
            {
                if (_repeatMode != value)
                {
                    _repeatMode = value;
                    OnPropertyChanged(nameof(RepeatModeState));
                }
            }
        }
        private bool _isSeeking;
        public double PositionInSeconds
        {
            get => _positionInSeconds;
            set
            {
                if (_isSeeking)
                    return;

                if (IsDragging)
                {
                    _positionInSeconds = value;
                    OnPropertyChanged(nameof(PositionInSeconds));
                    return;
                }

                if (_mediaPlayer.Source != null)
                {
                    _isSeeking = true;
                    try
                    {
                        _mediaPlayer.Position = TimeSpan.FromSeconds(value);
                        _positionInSeconds = value;
                    }
                    finally
                    {
                        _isSeeking = false;
                    }
                }
            }
        }
        public double DurationInSeconds => _mediaPlayer.NaturalDuration.HasTimeSpan ? _mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds : 0;
        public double Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                _mediaPlayer.Volume = _volume;
                OnPropertyChanged(nameof(Volume));
            }
        }

        public ObservableCollection<Song> Playlist => _playlist;
        public int CurrentIndex => _currentIndex;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
