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
        public async void ShowNotification(string message, bool isFavoriteLocal)
        {
            NotificationText.Text = message;
            NotificationBorder.Visibility = Visibility.Visible;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));
            NotificationBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            await Task.Delay(3000);

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.Completed += (s, e) => NotificationBorder.Visibility = Visibility.Collapsed;
            NotificationBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void SongsContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (!(sender is ContextMenu cm)) return;

            if (!(DataContext is MainViewModel vm)) return;

            var list = cm.PlacementTarget as ListView;
            Song song = list?.SelectedItem as Song;
            if (song == null)
            {
                var addMenu = cm.Items.OfType<MenuItem>().FirstOrDefault(mi => mi.Name == "AddToPlaylistMenu");
                var remMenu = cm.Items.OfType<MenuItem>().FirstOrDefault(mi => mi.Name == "RemoveFromPlaylistMenu");
                if (addMenu != null) { addMenu.Items.Clear(); addMenu.IsEnabled = false; }
                if (remMenu != null) { remMenu.Items.Clear(); remMenu.IsEnabled = false; }
                return;
            }

            var addMenuItem = cm.Items.OfType<MenuItem>().FirstOrDefault(mi => mi.Name == "AddToPlaylistMenu");
            var removeMenuItem = cm.Items.OfType<MenuItem>().FirstOrDefault(mi => mi.Name == "RemoveFromPlaylistMenu");

            if (addMenuItem != null)
            {
                addMenuItem.Items.Clear();
                // Для каждого плейлиста — добавляем подпункт; если песня уже в плейлисте — отключаем пункт
                foreach (var pl in vm.Playlists)
                {
                    // Проверяем наличие песни в плейлисте — читаем из БД (на случай, если vm.Playlists[].Songs не заполнен)
                    var plSongs = vm._dbService.GetPlaylistSongs(pl.PlaylistId);
                    bool contains = plSongs?.Any(s => s.SongId == song.SongId) ?? false;

                    var sub = new MenuItem { Header = pl.Name, IsEnabled = !contains };
                    // назначаем команду или обработчик
                    if (vm.AddToPlaylistCommand != null)
                    {
                        sub.Command = vm.AddToPlaylistCommand;
                        sub.CommandParameter = Tuple.Create(pl, song);
                    }
                    else
                    {
                        // если команды нет, повесим Click
                        sub.Click += (_, __) => vm.AddToPlaylist(Tuple.Create(pl, song));
                    }
                    addMenuItem.Items.Add(sub);
                }
                addMenuItem.IsEnabled = addMenuItem.Items.Count > 0;
            }

            if (removeMenuItem != null)
            {
                removeMenuItem.Items.Clear();
                // Добавим только те плейлисты, где песня есть
                bool any = false;
                foreach (var pl in vm.Playlists)
                {
                    var plSongs = vm._dbService.GetPlaylistSongs(pl.PlaylistId);
                    bool contains = plSongs?.Any(s => s.SongId == song.SongId) ?? false;
                    if (!contains) continue;

                    any = true;
                    var sub = new MenuItem { Header = pl.Name, IsEnabled = true };
                    if (vm.RemoveFromPlaylistCommand != null)
                    {
                        sub.Command = vm.RemoveFromPlaylistCommand;
                        sub.CommandParameter = Tuple.Create(pl, song);
                    }
                    else
                    {
                        sub.Click += (_, __) => vm.RemoveFromPlaylist(Tuple.Create(pl, song));
                    }
                    removeMenuItem.Items.Add(sub);
                }

                if (!any)
                {
                    // если ни в одном — покажем disabled элемент "Нет в плейлистах"
                    var none = new MenuItem { Header = "Песня не найдена в плейлистах", IsEnabled = false };
                    removeMenuItem.Items.Add(none);
                }
                removeMenuItem.IsEnabled = removeMenuItem.Items.Count > 0;
            }
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
