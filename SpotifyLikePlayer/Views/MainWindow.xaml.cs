using SpotifyLikePlayer.Models;
using SpotifyLikePlayer.ViewModels;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

using System.Windows.Controls.Primitives;
using System.Collections.ObjectModel;
using System.Windows.Media.Animation;

namespace SpotifyLikePlayer
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; set; }
        public MainWindow(MainViewModel vm)
        {
            ViewModel = vm;
            DataContext = ViewModel;
            InitializeComponent();
            ProgressSlider.MouseMove += ProgressSlider_MouseMove;
            ToolTipService.SetInitialShowDelay(ProgressSlider, 0);
        }

        private void PlaylistsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PlaylistsListBox.SelectedItem is Playlist selectedPlaylist)
            {
                var vm = DataContext as MainViewModel;
                vm?.LoadPlaylistSongs(selectedPlaylist);
            }
        }
        public async void ShowNotification(string message, bool isPositive)
        {
            if (NotificationText == null)
                return;

            // Меняем цвет текста в зависимости от статуса
            NotificationText.Foreground = isPositive ? Brushes.LimeGreen : Brushes.OrangeRed;
            NotificationText.Text = message;
            NotificationText.Opacity = 1;
            NotificationText.Visibility = Visibility.Visible;

            // Плавное появление
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.3)
            };
            NotificationText.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Показывается 2 секунды
            await Task.Delay(2000);

            // Плавное исчезновение
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.5),
                FillBehavior = FillBehavior.Stop
            };

            fadeOut.Completed += (s, e) =>
            {
                NotificationText.Visibility = Visibility.Collapsed;
            };

            NotificationText.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is MainViewModel vm)) return;

            if (sender is Button button && button.DataContext is Song song)
            {
                // Выполним команду (MVVM way)
                var cmd = vm.AddToFavoritesCommand;
                if (cmd != null && cmd.CanExecute(song))
                {
                    cmd.Execute(song);
                    return;
                }

                // Альтернатива: вызвать публичный метод VM (если есть)
                // vm.ToggleFavorite(song);
            }
        }

        private void Playlist_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var viewModel = (MainViewModel)DataContext;
            if (viewModel.SelectedPlaylist != null)
            {
                viewModel.LoadPlaylistSongs(viewModel.SelectedPlaylist); // Вызываем метод для загрузки песен
            }
            else
            {
                viewModel.Songs = viewModel._dbService.GetSongs() ?? new ObservableCollection<Song>(); // Возвращаемся к полному списку
                viewModel.OnPropertyChanged(nameof(viewModel.Songs));
            }
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            ViewModel.SearchSongs(textBox?.Text);
        }

        private void ProgressSlider_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Slider slider)
            {
                // Рассчитываем позицию курсора относительно слайдера
                var position = e.GetPosition(slider);
                double ratio = position.X / slider.ActualWidth;
                double hoveredSeconds = slider.Maximum * Math.Max(0, Math.Min(1, ratio)); // Ограничиваем 0-1
                TimeSpan hoveredTime = TimeSpan.FromSeconds(hoveredSeconds);
                string timeString = hoveredTime.ToString("mm\\:ss");

                // Обновляем ToolTip в реальном времени
                ToolTipService.SetToolTip(slider, timeString);
                ToolTipService.SetIsEnabled(slider, true);
                ToolTipService.SetPlacement(slider, PlacementMode.Relative);
                ToolTipService.SetVerticalOffset(slider, -30); // Смещение для видимости
                ToolTipService.SetHorizontalOffset(slider, position.X - 20); // Следует за курсором
                ToolTipService.SetShowDuration(slider, 10000); // Показывать 10 секунд
            }
        }

        private void SongsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SongsListView.SelectedItem is Song selectedSong)
            {
                var viewModel = (MainViewModel)DataContext;
                var playlistToUse = viewModel.SelectedPlaylist != null ? viewModel._dbService.GetPlaylistSongs(viewModel.SelectedPlaylist.PlaylistId) : viewModel.Songs;
                int songIndex = playlistToUse.ToList().FindIndex(s => s.SongId == selectedSong.SongId);
                viewModel.PlayerService.Play(selectedSong, playlistToUse, songIndex);
            }
        }

        public void ScrollToSelectedSong()
        {
            if (SongsListView.SelectedItem != null)
            {
                SongsListView.ScrollIntoView(SongsListView.SelectedItem);
            }
        }
    }
}
