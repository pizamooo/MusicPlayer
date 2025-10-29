﻿using System;
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

        private readonly Random _random = new Random();

        public event Action<Song> SongChanged;
        public event PropertyChangedEventHandler PropertyChanged;

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
            CompositionTarget.Rendering += OnRendering;
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
            if (_mediaPlayer.Source != null && _isPlaying)
            {
                _positionInSeconds = _mediaPlayer.Position.TotalSeconds;
                OnPropertyChanged(nameof(PositionInSeconds));
                OnPropertyChanged(nameof(DurationInSeconds));
            }
        }

        public void Play(Song song, ObservableCollection<Song> playlist, int index)
        {
            _playlist = playlist;
            _currentIndex = index;
            _mediaPlayer.Open(new Uri(song.FilePath));
            _mediaPlayer.Position = TimeSpan.FromSeconds(0); // Сбрасываем на начало
            _mediaPlayer.Play();
            _isPlaying = true;
            _isPaused = false;
            _positionInSeconds = 0;
            CurrentSong = song;

            OnPropertyChanged(nameof(CurrentSong));
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(IsPaused));

            OnSongChanged(song);
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
            if (_playlist == null || _playlist.Count == 0) return;

            if (_isShuffleEnabled)
            {
                int nextIndex = _random.Next(0, _playlist.Count);
                Play(_playlist[nextIndex], _playlist, nextIndex);
            }
            else
            {
                _currentIndex++;
                if (_currentIndex >= _playlist.Count)
                {
                    if (_repeatMode == RepeatMode.All)
                        _currentIndex = 0;
                    else
                        return; // конец плейлиста
                }
                Play(_playlist[_currentIndex], _playlist, _currentIndex);
            }
        }

        public void PlayPrevious()
        {
            if (_playlist == null || _playlist.Count == 0) return;

            if (_isShuffleEnabled)
            {
                int prevIndex = _random.Next(0, _playlist.Count);
                Play(_playlist[prevIndex], _playlist, prevIndex);
            }
            else
            {
                _currentIndex--;
                if (_currentIndex < 0)
                {
                    if (_repeatMode == RepeatMode.All)
                        _currentIndex = _playlist.Count - 1;
                    else
                        return; // начало плейлиста
                }
                Play(_playlist[_currentIndex], _playlist, _currentIndex);
            }
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
        public double PositionInSeconds
        {
            get => _positionInSeconds;
            set
            {
                if (_mediaPlayer.Source != null)
                {
                    _positionInSeconds = value;
                    _mediaPlayer.Position = TimeSpan.FromSeconds(value);
                    OnPropertyChanged(nameof(PositionInSeconds));
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
