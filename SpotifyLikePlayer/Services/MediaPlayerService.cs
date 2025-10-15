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

namespace SpotifyLikePlayer.Services
{
    public class MediaPlayerService : INotifyPropertyChanged
    {
        private readonly MediaPlayer _mediaPlayer = new MediaPlayer();
        private ObservableCollection<Song> _playlist;
        private int _currentIndex;
        private bool _isPlaying;
        private double _positionInSeconds;
        private double _volume = 0.5;

        public event PropertyChangedEventHandler PropertyChanged;

        public MediaPlayerService()
        {
            _mediaPlayer.MediaEnded += (s, e) => PlayNext();
            CompositionTarget.Rendering += OnRendering;
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (_mediaPlayer.Source != null && _isPlaying)
            {
                _positionInSeconds = _mediaPlayer.Position.TotalSeconds;
                OnPropertyChanged(nameof(PositionInSeconds));
                OnPropertyChanged(nameof(DurationInSeconds)); // Обновляем на случай изменений
            }
        }

        public void Play(Song song, ObservableCollection<Song> playlist, int index)
        {
            _playlist = playlist;
            _currentIndex = index;
            _mediaPlayer.Open(new Uri(song.FilePath));
            _mediaPlayer.Play();
            _isPlaying = true;
            OnPropertyChanged(nameof(CurrentSong));
            OnPropertyChanged(nameof(IsPlaying));
        }

        public void Pause()
        {
            _mediaPlayer.Pause();
            _isPlaying = false;
            OnPropertyChanged(nameof(IsPlaying));
        }

        public void PlayNext()
        {
            if (_playlist != null && _currentIndex < _playlist.Count - 1)
            {
                _currentIndex++;
                Play(_playlist[_currentIndex], _playlist, _currentIndex);
            }
        }

        public void PlayPrevious()
        {
            if (_playlist != null && _currentIndex > 0)
            {
                _currentIndex--;
                Play(_playlist[_currentIndex], _playlist, _currentIndex);
            }
        }

        public Song CurrentSong => _playlist?[_currentIndex];
        public bool IsPlaying => _isPlaying;
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

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
