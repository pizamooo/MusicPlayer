using MaterialDesignThemes.Wpf;
using SpotifyLikePlayer.Models;
using SpotifyLikePlayer.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SpotifyLikePlayer.Views
{
    /// <summary>
    /// Логика взаимодействия для AdminPanelWindow.xaml
    /// </summary>
    public partial class AdminPanelWindow : Window
    {
        private readonly DatabaseService _dbService = new DatabaseService();
        private readonly MusicSubmissionService _submissionService = new MusicSubmissionService();
        private readonly MediaPlayerService _playerService;
        public MediaPlayerService PlayerService => _playerService;
        private bool _isDragging = false;

        public static readonly DependencyProperty CurrentTempSongProperty = DependencyProperty.Register("CurrentTempSong", typeof(Song), typeof(AdminPanelWindow));

        public Song CurrentTempSong
        {
            get => (Song)GetValue(CurrentTempSongProperty);
            set => SetValue(CurrentTempSongProperty, value);
        }

        private ObservableCollection<Song> TempPlaylist { get; set; } = new ObservableCollection<Song>();

        public ICommand TogglePlayPauseCommand { get; }
        public AdminPanelWindow()
        {
            InitializeComponent();
            LoadAllSubmissions();
            DataContext = this;

            _playerService = new MediaPlayerService();
            SetupPlayerBindings();
            PlayPauseButton.DataContext = _playerService;
            ProgressSlider.DataContext = _playerService;
        }

        private void ProgressSlider_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isDragging = true;
        }

        private void ProgressSlider_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isDragging = false;

            _playerService.Seek(ProgressSlider.Value);
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isDragging)
            {
                return;
            }
        }

        private void PendingRequestsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var submission = (sender as ListBox)?.SelectedItem as MusicSubmission;
            if (submission == null) return;

            PlaySubmission(submission);
        }

        private void ApprovedRequestsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("Проигрывание доступно только для заявок в ожидании.","Недоступно", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RejectedRequestsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("Проигрывание доступно только для заявок в ожидании.","Недоступно", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PlaySubmission(MusicSubmission submission)
        {
            try
            {
                var tempSong = TempPlaylist.FirstOrDefault(s => s.Title == submission.Title && s.Artist.Name == submission.ArtistName);
                if (tempSong == null)
                {
                    MessageBox.Show("Песня не найдена в плейлисте.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int index = TempPlaylist.IndexOf(tempSong);

                _playerService.Play(tempSong, TempPlaylist, index);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка воспроизведения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupPlayerBindings()
        {
            _playerService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MediaPlayerService.IsPlaying))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        PlayIcon.Visibility = _playerService.IsPlaying ? Visibility.Collapsed : Visibility.Visible;
                        PauseIcon.Visibility = _playerService.IsPlaying ? Visibility.Visible : Visibility.Collapsed;
                    });
                }
                else if (e.PropertyName == nameof(MediaPlayerService.PositionInSeconds))
                {
                    if (!_isDragging)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ProgressSlider.Value = _playerService.PositionInSeconds;
                        });
                    }
                }
                else if (e.PropertyName == nameof(MediaPlayerService.DurationInSeconds))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ProgressSlider.Maximum = _playerService.DurationInSeconds;
                    });
                }
                else if (e.PropertyName == nameof(MediaPlayerService.CurrentSong))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CurrentTempSong = _playerService.CurrentSong;
                    });
                }
            };
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_playerService == null)
                return;

            if (_playerService.IsPlaying)
            {
                _playerService.Pause();
                PlayIcon.Visibility = Visibility.Visible;
                PauseIcon.Visibility = Visibility.Collapsed;
            }
            else if (_playerService.IsPaused)
            {
                _playerService.Resume();
                PlayIcon.Visibility = Visibility.Collapsed;
                PauseIcon.Visibility = Visibility.Visible;
            }
            else if (_playerService.CurrentSong != null)
            {
                int index = TempPlaylist.IndexOf(_playerService.CurrentSong);
                if (index >= 0)
                {
                    _playerService.Play(_playerService.CurrentSong, TempPlaylist, index);
                    PlayIcon.Visibility = Visibility.Collapsed;
                    PauseIcon.Visibility = Visibility.Visible;
                }
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            _playerService.PlayNext();
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            _playerService.PlayPrevious();
        }

        private void ListenToSong_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var submission = button?.CommandParameter as MusicSubmission;
            if (submission != null)
            {
                PlaySubmission(submission);
            }
        }

        private void CleanupTempFiles()
        {
            string tempFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SpotifyLikePlayer_MusicSubmissions");
            if (Directory.Exists(tempFolder))
            {
                try
                {
                    Directory.Delete(tempFolder, true);
                }
                catch { }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _playerService.Pause();
            CleanupTempFiles();
            AnimateAndClose();
        }

        private void LoadAllSubmissions()
        {
            try
            {
                var pending = _submissionService.GetSubmissionsByStatus("Pending");
                var approved = _submissionService.GetSubmissionsByStatus("Approved");
                var rejected = _submissionService.GetSubmissionsByStatus("Rejected");

                PendingRequestsListBox.ItemsSource = pending;
                ApprovedRequestsListBox.ItemsSource = approved;
                RejectedRequestsListBox.ItemsSource = rejected;

                TempPlaylist.Clear();
                AddSubmissionsToPlaylist(pending);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки заявок: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddSubmissionsToPlaylist(IEnumerable<MusicSubmission> submissions)
        {
            foreach (var submission in submissions)
            {
                string tempPath = _submissionService.SaveTempFile(submission);
                if (!string.IsNullOrEmpty(tempPath))
                {
                    var tempSong = new Song
                    {
                        Title = submission.Title,
                        Artist = new Artist { Name = submission.ArtistName },
                        FilePath = tempPath,
                        CoverImage = submission.CoverImageSource,
                        Duration = TimeSpan.Zero
                    };
                    TempPlaylist.Add(tempSong);
                }
            }
        }

        private void ApproveRequest_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var submission = button?.CommandParameter as MusicSubmission;
            if (submission == null) return;

            try
            {
                _submissionService.ApproveSubmission(submission.SubmissionId);
                MessageBox.Show($"Заявка на '{submission.Title}' одобрена и добавлена в библиотеку.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadAllSubmissions();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка одобрения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RejectRequest_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var submission = button?.CommandParameter as MusicSubmission;
            if (submission == null) return;

            try
            {
                _submissionService.RejectSubmission(submission.SubmissionId);
                MessageBox.Show($"Заявка на '{submission.Title}' отклонена.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadAllSubmissions();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отклонения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteRequest_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var submission = button?.CommandParameter as MusicSubmission;
            if (submission == null) return;

            if (MessageBox.Show($"Вы уверены, что хотите удалить заявку на '{submission.Title}'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    _submissionService.DeleteSubmission(submission.SubmissionId);
                    MessageBox.Show($"Заявка на '{submission.Title}' удалена.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadAllSubmissions();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteAllPending_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Удалить все заявки со статусом 'Pending'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    _submissionService.DeleteAllSubmissionsByStatus("Pending");
                    MessageBox.Show("Все заявки 'Pending' удалены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadAllSubmissions();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteAllApproved_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Удалить все заявки со статусом 'Approved'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    _submissionService.DeleteAllSubmissionsByStatus("Approved");
                    MessageBox.Show("Все заявки 'Approved' удалены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadAllSubmissions();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteAllRejected_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Удалить все заявки со статусом 'Rejected'?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    _submissionService.DeleteAllSubmissionsByStatus("Rejected");
                    MessageBox.Show("Все заявки 'Rejected' удалены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadAllSubmissions();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AnimateAndClose()
        {
            var sb = new Storyboard();

            var scaleTransform = new ScaleTransform(1.0, 1.0);
            this.RenderTransformOrigin = new Point(0.5, 0.5);
            this.RenderTransform = scaleTransform;

            var scaleX = new DoubleAnimation
            {
                To = 0.9,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(scaleX, this);
            Storyboard.SetTargetProperty(scaleX, new PropertyPath("RenderTransform.(ScaleTransform.ScaleX)"));

            var scaleY = new DoubleAnimation
            {
                To = 0.9,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(scaleY, this);
            Storyboard.SetTargetProperty(scaleY, new PropertyPath("RenderTransform.(ScaleTransform.ScaleY)"));

            var fadeOut = new DoubleAnimation
            {
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            Storyboard.SetTarget(fadeOut, this);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(Window.OpacityProperty));

            sb.Children.Add(scaleX);
            sb.Children.Add(scaleY);
            sb.Children.Add(fadeOut);

            sb.Completed += (s, e) => this.Close();

            sb.Begin();
        }
    }
}
