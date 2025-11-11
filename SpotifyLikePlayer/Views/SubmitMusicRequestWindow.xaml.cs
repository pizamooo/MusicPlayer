using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using SpotifyLikePlayer.Models;
using System.IO;
using SpotifyLikePlayer.Services;
using System.Windows.Media.Animation;

namespace SpotifyLikePlayer.Views
{
    /// <summary>
    /// Логика взаимодействия для SubmitMusicRequestWindow.xaml
    /// </summary>
    public partial class SubmitMusicRequestWindow : Window
    {
        private readonly User _artist;
        private byte[] _coverBytes;
        private byte[] _mp3Bytes;
        private string _selectedMp3Path;
        private int _detectedYear = DateTime.Now.Year;
        public SubmitMusicRequestWindow(User artist)
        {
            InitializeComponent();
            _artist = artist;
            TitleBox.Text = "";
        }

        private void BrowseMp3Button_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "MP3 files (*.mp3)|*.mp3",
                Title = "Выберите трек"
            };
            if (dialog.ShowDialog() == true)
            {
                _selectedMp3Path = dialog.FileName;
                Mp3PathText.Text = System.IO.Path.GetFileName(_selectedMp3Path);

                try
                {
                    _mp3Bytes = File.ReadAllBytes(_selectedMp3Path);

                    using (var file = TagLib.File.Create(_selectedMp3Path))
                    {
                        if (file.Tag.Year > 0)
                            _detectedYear = (int)file.Tag.Year;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка чтения файла: {ex.Message}");
                }
            }
        }

        private void BrowseCoverButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Images|*.jpg;*.jpeg;*.png",
                Title = "Обложка"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _coverBytes = File.ReadAllBytes(dialog.FileName);
                    using (var ms = new MemoryStream(_coverBytes))
                    {
                        var img = new BitmapImage();
                        img.BeginInit();
                        img.StreamSource = ms;
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.EndInit();
                        img.Freeze();
                        CoverImage.Source = img;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки обложки: {ex.Message}");
                }
            }
        }

        private void UseDefaultCover_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string defaultPath = "C:\\Users\\dobry\\source\\repos\\SpotifyLikePlayer\\SpotifyLikePlayer\\music.png";
                _coverBytes = File.ReadAllBytes(defaultPath);
                using (var ms = new MemoryStream(_coverBytes))
                {
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.StreamSource = ms;
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.EndInit();
                    img.Freeze();
                    CoverImage.Source = img;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось загрузить стандартную обложку: {ex.Message}");
            }
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            if (_mp3Bytes == null)
            {
                MessageBox.Show("Выберите MP3-файл.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(TitleBox.Text))
            {
                MessageBox.Show("Укажите название трека.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_coverBytes == null)
            {
                MessageBox.Show("Выберите обложку или используйте стандартную.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (GenreComboBox.SelectedItem == null)
            {
                MessageBox.Show("Пожалуйста, выберите жанр.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ConfirmAuthorshipCheckBox.IsChecked == true)
            {
                MessageBox.Show("Подтвердите, что вы являетесь автором музыки.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var service = new MusicSubmissionService();
                string albumTitle = AlbumTitleBox.Text?.Trim();
                string selectedGenre = (GenreComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
                service.SubmitMusicRequest(
                    _artist.UserId,
                    TitleBox.Text.Trim(),
                    selectedGenre,
                    _mp3Bytes,
                    _coverBytes,
                    _detectedYear,
                    albumTitle
                );
                MessageBox.Show("Заявка отправлена на модерацию!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка отправки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            AnimateAndClose();
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
