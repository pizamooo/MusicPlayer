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
using System.Drawing;
using System.IO;

namespace SpotifyLikePlayer
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<Song> _lastSearchResults = new List<Song>();
        private MainViewModel _vm;

        public MainViewModel ViewModel { get; set; }
        public MainWindow(MainViewModel vm)
        {
            ViewModel = vm;
            _vm = vm;
            DataContext = ViewModel;
            InitializeComponent();
            ProgressSlider.MouseMove += ProgressSlider_MouseMove;
            ToolTipService.SetInitialShowDelay(ProgressSlider, 0);

            SuggestionsList.PreviewMouseWheel += SuggestionsList_PreviewMouseWheel;
            SuggestionsList.PreviewMouseLeftButtonUp += SuggestionsList_PreviewMouseLeftButtonUp;

            ViewModel.PlayerService.SongChanged += OnSongChanged;

        }

        private void UpdateCurrentSongHighlight()
        {
            var currentSong = ViewModel.PlayerService.CurrentSong;
            if (currentSong == null) return;

            var match = ViewModel.Songs.FirstOrDefault(s => s.SongId == currentSong.SongId);
            if (match != null)
            {
                SongsListView.SelectedItem = match;
                SongsListView.ScrollIntoView(match);
            }
        }

        private void DownloadSong_Click(object sender, RoutedEventArgs e)
        {
            if (!(SongsListView.SelectedItem is Song song))
            {
                ShowNotification("Выберите песню для скачивания", false);
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(song.FilePath))
                {
                    ShowNotification("Невозможно скачать: отсутствует путь к файлу.", false);
                    return;
                }

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = $"Сохранить песню - {song.Title}",
                    FileName = $"{song.Title} - {song.Artist?.Name ?? "Неизвестный исполнитель"}",
                    Filter = "Аудиофайл (*.mp3)|*.mp3|Все файлы (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    File.Copy(song.FilePath, dialog.FileName, overwrite: true);
                    ShowNotification($"Песня '{song.Title}' успешно скачана!", true);
                }
            }
            catch (Exception ex)
            {
                ShowNotification($"Ошибка при скачивании: {ex.Message}", false);
            }
        }

        private void SongsListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (SongsListView.View is GridView gridView)
            {
                double totalWidth = SongsListView.ActualWidth - 100 - 90 - 60 - 50;

                if (totalWidth <= 0) return;

                double eachWidth = totalWidth / 4;
                gridView.Columns[1].Width = eachWidth; 
                gridView.Columns[2].Width = eachWidth; 
                gridView.Columns[3].Width = eachWidth; 
                gridView.Columns[4].Width = eachWidth; 
            }
        }

        private void ProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var currentUser = _vm.CurrentUser;

            if (currentUser == null)
            {
                ShowNotification("Ошибка: пользователь не найден", false);
                return;
            }

            var profileWindow = new Views.ProfileWindow(currentUser)
            {
                Owner = this
            };

            if (profileWindow.ShowDialog() == true)
            {
                ShowNotification("Профиль успешно обновлён", true);
            }
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            string info =
                "🎧 PATHTRACK — музыкальный плеер.\n\n" +
                "Возможности:\n" +
                "• Воспроизведение музыки с повтором и перемешиванием\n" +
                "• Создание и удаление плейлистов\n" +
                "• Добавление песен в избранное\n" +
                "• Скачивание песен\n" +
                "• Быстрый поиск по названию, артисту и альбому\n" +
                "• Просмотр информации о треке и альбоме\n" +
                "• Автоматическое сохранение состояния\n\n" +

                "Как пользоваться плеером?\n" +
                "• Правая кнопка мыши - выбор действия с песней\n" +
                "• Нижние кнопки - выбор метода прослушивания трека\n" +
                "• Нажатие на звезду - создание плейлиста и добавление в Favorite\n" +
                "• Левая кнопка мыши по плейлистам - выбрать плейлист для прослушивания\n" +
                "• Поисковая строка - нахождение вашей любимой песни\n" +
                "• Изменить жанр - изменение жанра музыки для вас\n\n" +
                "Разработчик: pizamooo\n" +
                "Версия: 1.0\n\n" +
                "Спасибо за использование приложения!";

            MessageBox.Show(info, "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            var allSongs = ViewModel._dbService.GetSongs() ?? new ObservableCollection<Song>();
            ViewModel.Songs.Clear();
            foreach (var s in allSongs)
                ViewModel.Songs.Add(s);

            ViewModel.SelectedPlaylist = null;
            ViewModel.PlayerService.UpdatePlaylist(ViewModel.Songs);
            UpdateMusicContextText("Вся музыка");
            ViewModel.OnPropertyChanged(nameof(ViewModel.Songs));
            Dispatcher.InvokeAsync(() =>
            {
                UpdateCurrentSongHighlight();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void UpdateMusicContextText(string text)
        {
            string newText;

            if (ViewModel.SelectedPlaylist != null)
            {
                newText = $"Плейлист: {ViewModel.SelectedPlaylist.Name}";
            }
            else if (ViewModel.SelectedSong?.Album != null)
            {
                newText = $"Альбом: {ViewModel.SelectedSong.Album.Title}";
            }
            else
            {
                newText = $"Вся музыка";
            }
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, _) =>
            {
                MusicContextText.Text = newText;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
                MusicContextText.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            };
            MusicContextText.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            var player = ViewModel.PlayerService;
            player.ToggleShuffle();

            ShuffleIcon.Foreground = player.IsShuffleEnabled ? System.Windows.Media.Brushes.LightSkyBlue : System.Windows.Media.Brushes.White;
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            var player = ViewModel.PlayerService;
            player.ToggleRepeatMode();

            switch (player.RepeatModeState)
            {
                case SpotifyLikePlayer.Services.MediaPlayerService.RepeatMode.None:
                    RepeatIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Repeat;
                    RepeatIcon.Foreground = System.Windows.Media.Brushes.White;
                    break;

                case SpotifyLikePlayer.Services.MediaPlayerService.RepeatMode.All:
                    RepeatIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Repeat;
                    RepeatIcon.Foreground = System.Windows.Media.Brushes.LightSkyBlue;
                    break;

                case SpotifyLikePlayer.Services.MediaPlayerService.RepeatMode.One:
                    RepeatIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.RepeatOnce;
                    RepeatIcon.Foreground = System.Windows.Media.Brushes.DeepSkyBlue;
                    break;
            }
        }
        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            HideSongInfo();
        }

        private void OnSongChanged(Song song)
        {
            Dispatcher.Invoke(() =>
            {
                if (song == null)
                    return;

                var match = ViewModel.Songs.FirstOrDefault(s => s.SongId == song.SongId);
                if (match != null)
                {
                    SongsListView.SelectedItem = match;
                    SongsListView.ScrollIntoView(match);

                    foreach (var s in ViewModel.Songs)
                    {
                        s.IsPlaying = (s.SongId == song.SongId);
                    }

                    SongsListView.UpdateLayout();
                }

                ViewModel.CurrentSong = song;
            });
        }

        public void OnSongChangedExternally()
        {
            HideSongInfo();
        }

        private void ShowSongInfo()
        {
            if (ViewModel.SelectedSong == null)
                return;

            if (SongInfoPanel.Visibility != Visibility.Visible)
            {
                SongInfoPanel.Visibility = Visibility.Visible;

                var animY = new DoubleAnimation
                {
                    From = -150,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                SongInfoPanel.RenderTransform = new TranslateTransform();
                SongInfoPanel.RenderTransform.BeginAnimation(TranslateTransform.YProperty, animY);
            }
        }

        private void SongsListView_ShowDetails_Click(object sender, RoutedEventArgs e)
        {
            if (SongsListView.SelectedItem is Song song)
            {
                ViewModel.SelectedSong = song;
                ShowSongInfo();
            }
        }

        private void HideSongInfo()
        {
            if (SongInfoPanel.Visibility != Visibility.Visible)
                return;

            var animY = new DoubleAnimation
            {
                From = 0,
                To = -150,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            animY.Completed += (s, _) =>
            {
                SongInfoPanel.Visibility = Visibility.Collapsed;
            };

            SongInfoPanel.RenderTransform = new TranslateTransform();
            SongInfoPanel.RenderTransform.BeginAnimation(TranslateTransform.YProperty, animY);
        }

        private void CloseSongInfo_Click(object sender, RoutedEventArgs e)
        {
            HideSongInfo();
        }

        private void GoToAlbum_Click(object sender, RoutedEventArgs e)
        {
            var selected = ViewModel?.SelectedSong;
            if (selected?.Album == null)
                return;

            int albumId = selected.Album.AlbumId;

            var albumSongs = ViewModel._dbService
                .GetSongs()
                ?.Where(s => s.AlbumId == albumId)
                .ToList() ?? new List<Song>();

            if (!albumSongs.Any())
                return;

            UpdateSongsList(albumSongs);
            ViewModel.UpdateFavoriteFlags();

            var currentSong = ViewModel.PlayerService.CurrentSong;
            if (currentSong != null)
            {
                var match = albumSongs.FirstOrDefault(s => s.SongId == currentSong.SongId);
                if (match != null)
                {
                    SongsListView.SelectedItem = match;
                    SongsListView.ScrollIntoView(match);
                }
                else
                {
                    var first = albumSongs.First();
                    SongsListView.SelectedItem = first;
                    SongsListView.ScrollIntoView(first);
                }
            }
            ViewModel.SelectedSong = selected;
            UpdateMusicContextText("");

            HideSongInfo();

            ViewModel.SyncSelectedSongWithCurrent();
            ViewModel.PlayerService.UpdatePlaylist(ViewModel.Songs);
        }

        public void OnTrackSwitched()
        {
            HideSongInfo();
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = SearchBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                SuggestionsPopup.IsOpen = false;
                ResetSongsList();
                UpdateCurrentSongHighlight();
                Keyboard.ClearFocus();
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
            UpdateCurrentSongHighlight();
            ViewModel.PlayerService.UpdatePlaylist(ViewModel.Songs);
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

                SuggestionsPopup.IsOpen = false;

                SearchBox.Text = selected.Title;

                UpdateSongsList(new List<Song> { selected });

                SongsListView.SelectedItem = selected;
                SongsListView.ScrollIntoView(selected);

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
                if (SuggestionsPopup.IsOpen && SuggestionsList.SelectedItem is Song selectedSong)
                {
                    SelectSuggestion(selectedSong);
                    SuggestionsPopup.IsOpen = false;
                    e.Handled = true;
                    return;
                }

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

            int delta = e.Delta > 0 ? -1 : 1;
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
                ViewModel.UpdateFavoriteFlags();
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
                ViewModel.UpdateFavoriteFlags();
                UpdateCurrentSongHighlight();
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
            var menu = new ContextMenu();

            var genres = new List<string> { "Hip-Hop", "Rock", "Metal", "Pop", "Alt Metal" };

            foreach (var genre in genres)
            {
                var item = new MenuItem
                {
                    Header = genre,
                    Foreground = System.Windows.Media.Brushes.Lime,
                    FontSize = 14,
                    Icon = new MaterialDesignThemes.Wpf.PackIcon
                    {
                        Kind = MaterialDesignThemes.Wpf.PackIconKind.MusicNote,
                        Foreground = System.Windows.Media.Brushes.Lime,
                        Width = 18,
                        Height = 18
                    }
                };

                item.Click += (s, ev) =>
                {
                    ViewModel.LoadSongsByGenre(genre);
                    UpdateCurrentSongHighlight();
                    ShowNotification($"Показаны треки жанра: {genre}", true);
                    ViewModel.PlayerService.UpdatePlaylist(ViewModel.Songs);
                };

                menu.Items.Add(item);
            }

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
                PlaylistsListBox.SelectedItem = selectedPlaylist;
                vm.SelectedPlaylist = selectedPlaylist;
                UpdateCurrentSongHighlight();
            }
            UpdateMusicContextText("");
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
                foreach (var pl in vm.Playlists)
                {
                    var plSongs = vm._dbService.GetPlaylistSongs(pl.PlaylistId);
                    bool contains = plSongs?.Any(s => s.SongId == song.SongId) ?? false;

                    var sub = new MenuItem { Header = pl.Name, IsEnabled = !contains };
                    if (vm.AddToPlaylistCommand != null)
                    {
                        sub.Command = vm.AddToPlaylistCommand;
                        sub.CommandParameter = Tuple.Create(pl, song);
                    }
                    else
                    {
                        sub.Click += (_, __) => vm.AddToPlaylist(Tuple.Create(pl, song));
                    }
                    addMenuItem.Items.Add(sub);
                }
                addMenuItem.IsEnabled = addMenuItem.Items.Count > 0;
            }

            if (removeMenuItem != null)
            {
                removeMenuItem.Items.Clear();
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
                var cmd = vm.AddToFavoritesCommand;
                if (cmd != null && cmd.CanExecute(song))
                {
                    cmd.Execute(song);
                    return;
                }
            }
        }

        private void Playlist_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            UpdateMusicContextText("");

            if (ViewModel.SelectedPlaylist != null)
            {
                ViewModel.LoadPlaylistSongs(ViewModel.SelectedPlaylist);
                ViewModel.PlayerService.UpdatePlaylist(ViewModel.Songs);
            }

            Dispatcher.InvokeAsync(() =>
            {
                UpdateCurrentSongHighlight();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ProgressSlider_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var slider = sender as Slider;
                if (slider != null)
                {
                    var position = e.GetPosition(slider);
                    double value = (position.X / slider.ActualWidth) * (slider.Maximum - slider.Minimum) + slider.Minimum;
                    slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, value));
                }
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
