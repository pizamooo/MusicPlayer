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
            DataContext = new MainViewModel();
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            ViewModel.SearchSongs(textBox?.Text);
        }

        private void Playlist_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var viewModel = (MainViewModel)DataContext;
            if (viewModel.SelectedPlaylist != null)
            {
                viewModel.LoadPlaylistSongs(viewModel.SelectedPlaylist);
            }
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
    }
}
