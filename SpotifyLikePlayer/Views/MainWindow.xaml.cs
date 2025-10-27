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
        private List<Song> _lastSearchResults = new List<Song>();

        public MainViewModel ViewModel { get; set; }
        public MainWindow(MainViewModel vm)
        {
            ViewModel = vm;
            DataContext = ViewModel;
            InitializeComponent();
            ProgressSlider.MouseMove += ProgressSlider_MouseMove;
            ToolTipService.SetInitialShowDelay(ProgressSlider, 0);

            SuggestionsList.PreviewMouseWheel += SuggestionsList_PreviewMouseWheel;

            SuggestionsList.PreviewMouseLeftButtonUp += SuggestionsList_PreviewMouseLeftButtonUp;
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = SearchBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                SuggestionsPopup.IsOpen = false;
                ResetSongsList();
                return;
            }

            var results = await Task.Run(() =>
            {
                try
                {
                    var allSongs = ViewModel._dbService.GetSongs();
                    return allSongs
                        .Where(s =>
                            (s.Title ?? string.Empty).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            (s.Artist?.Name ?? string.Empty).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            (s.Album?.Title ?? string.Empty).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }
                catch
                {
                    return new List<Song>();
                }
            });

            _lastSearchResults = results;

            var suggestions = _lastSearchResults.Take(12).ToList();
            SuggestionsList.ItemsSource = suggestions;
            SuggestionsPopup.IsOpen = suggestions.Any();

            UpdateSongsList(results);
        }

        private void SuggestionsList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (SuggestionsList.SelectedItem is Song selected)
            {
                SelectSuggestion(selected);
                e.Handled = true;
            }
        }

        private void SuggestionsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (SuggestionsList.SelectedItem is Song selectedSong)
            {
                SelectSuggestion(selectedSong);
            }
        }

        private void SelectSuggestion(Song selected)
        {
            try
            {
                if (selected == null) return;

                // Закрываем popup
                SuggestionsPopup.IsOpen = false;

                // Ставим текст в SearchBox (для красоты)
                SearchBox.Text = selected.Title;

                // Обновляем список песен (можно все найденные или только эту)
                UpdateSongsList(new List<Song> { selected });

                // Выделяем её в таблице
                SongsListView.SelectedItem = selected;
                SongsListView.ScrollIntoView(selected);

                // Снимаем фокус с TextBox
                Keyboard.ClearFocus();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SelectSuggestion error: {ex}");
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Если открыт Popup и выбрана подсказка — применяем её
                if (SuggestionsPopup.IsOpen && SuggestionsList.SelectedItem is Song selectedSong)
                {
                    SelectSuggestion(selectedSong);
                    SuggestionsPopup.IsOpen = false;
                    e.Handled = true;
                    return;
                }

                // Иначе — выполняем обычный поиск по введённому тексту
                string query = SearchBox.Text?.Trim();
                if (string.IsNullOrEmpty(query))
                {
                    ResetSongsList();
                    SuggestionsPopup.IsOpen = false;
                    e.Handled = true;
                    return;
                }

                var allSongs = ViewModel._dbService.GetSongs();
                var found = allSongs
                    .Where(s =>
                        (s.Title ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (s.Artist?.Name ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (s.Album?.Title ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                UpdateSongsList(found);

                SuggestionsPopup.IsOpen = false;
                Keyboard.ClearFocus();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                // Навигация вниз по списку подсказок
                if (SuggestionsPopup.IsOpen && SuggestionsList.Items.Count > 0)
                {
                    int idx = SuggestionsList.SelectedIndex;
                    idx = Math.Min(SuggestionsList.Items.Count - 1, Math.Max(0, idx + 1));
                    SuggestionsList.SelectedIndex = idx;
                    SuggestionsList.ScrollIntoView(SuggestionsList.SelectedItem);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Up)
            {
                // Навигация вверх по списку подсказок
                if (SuggestionsPopup.IsOpen && SuggestionsList.Items.Count > 0)
                {
                    int idx = SuggestionsList.SelectedIndex;
                    idx = Math.Max(0, idx - 1);
                    SuggestionsList.SelectedIndex = idx;
                    SuggestionsList.ScrollIntoView(SuggestionsList.SelectedItem);
                    e.Handled = true;
                }
            }
        }

        private void SuggestionsList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!SuggestionsPopup.IsOpen) return;
            if (!(sender is ListBox lb)) return;
            if (lb.Items.Count == 0) return;

            int delta = e.Delta > 0 ? -1 : 1; // вверх уменьшает индекс
            int idx = lb.SelectedIndex;
            if (idx < 0) idx = 0;
            idx += delta;
            idx = Math.Max(0, Math.Min(lb.Items.Count - 1, idx));
            lb.SelectedIndex = idx;
            lb.ScrollIntoView(lb.SelectedItem);
            e.Handled = true;
        }

        private void UpdateSongsList(List<Song> songs)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ViewModel.Songs.Clear();
                foreach (var s in songs)
                    ViewModel.Songs.Add(s);

                ViewModel.OnPropertyChanged(nameof(ViewModel.Songs));
            });
        }

        private void ResetSongsList()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (ViewModel.SelectedPlaylist != null)
                {
                    var plSongs = ViewModel._dbService.GetPlaylistSongs(ViewModel.SelectedPlaylist.PlaylistId)
                                  ?? new ObservableCollection<Song>();
                    ViewModel.Songs.Clear();
                    foreach (var s in plSongs)
                        ViewModel.Songs.Add(s);
                }
                else
                {
                    var all = ViewModel._dbService.GetSongs() ?? new ObservableCollection<Song>();
                    ViewModel.Songs.Clear();
                    foreach (var s in all)
                        ViewModel.Songs.Add(s);
                }
                ViewModel.OnPropertyChanged(nameof(ViewModel.Songs));
            });
        }

        private void PerformSearch(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                ViewModel.Songs = ViewModel.SelectedPlaylist != null
                    ? ViewModel._dbService.GetPlaylistSongs(ViewModel.SelectedPlaylist.PlaylistId)
                    : ViewModel._dbService.GetSongs();
            }
            else
            {
                var allSongs = ViewModel._dbService.GetSongs();
                var foundSongs = allSongs
                    .Where(s =>
                        s.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.Artist.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        s.Album.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                ViewModel.Songs.Clear();
                foreach (var song in foundSongs)
                    ViewModel.Songs.Add(song);
            }

            ViewModel.OnPropertyChanged(nameof(ViewModel.Songs));
        }

        private void ChangeGenreButton_Click(object sender, RoutedEventArgs e)
        {
            // Создаем простое контекстное меню
            var menu = new ContextMenu();

            var genres = new List<string> { "Hip-Hop", "Rock", "Metal", "Pop" };

            foreach (var genre in genres)
            {
                var item = new MenuItem
                {
                    Header = genre,
                    Foreground = Brushes.Lime,
                    FontSize = 14,
                    Icon = new MaterialDesignThemes.Wpf.PackIcon
                    {
                        Kind = MaterialDesignThemes.Wpf.PackIconKind.MusicNote,
                        Foreground = Brushes.Lime,
                        Width = 18,
                        Height = 18
                    }
                };

                // при выборе жанра
                item.Click += (s, ev) =>
                {
                    // тут подгружаем песни по жанру
                    ViewModel.LoadSongsByGenre(genre);

                    // уведомление
                    ShowNotification($"Показаны треки жанра: {genre}", true);
                };

                menu.Items.Add(item);
            }

            // Привязываем контекстное меню к кнопке
            menu.PlacementTarget = ChangeGenreButton;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
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

        private void ProgressSlider_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Slider slider)
            {
                var position = e.GetPosition(slider);
                double ratio = position.X / slider.ActualWidth;
                double hoveredSeconds = slider.Maximum * Math.Max(0, Math.Min(1, ratio));
                TimeSpan hoveredTime = TimeSpan.FromSeconds(hoveredSeconds);
                string timeString = hoveredTime.ToString("mm\\:ss");

                ToolTipService.SetToolTip(slider, timeString);
                ToolTipService.SetIsEnabled(slider, true);
                ToolTipService.SetPlacement(slider, PlacementMode.Relative);
                ToolTipService.SetVerticalOffset(slider, -30);
                ToolTipService.SetHorizontalOffset(slider, position.X - 20);
                ToolTipService.SetShowDuration(slider, 10000);
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
