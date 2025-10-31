using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using BCrypt.Net;
using Microsoft.Win32;
using SpotifyLikePlayer.Models;

namespace SpotifyLikePlayer.Views
{
    /// <summary>
    /// Логика взаимодействия для ProfileWindow.xaml
    /// </summary>
    public partial class ProfileWindow : Window
    {
        private User _currentUser;
        private byte[] _newPhotoBytes = null;
        public readonly string _connectionString = ConfigurationManager.ConnectionStrings["MusicDB"].ConnectionString;

        public ProfileWindow(User user)
        {
            InitializeComponent();
            _currentUser = user ?? throw new ArgumentNullException(nameof(user));
            LoadUserData();
        }

        private BitmapImage DefaultAvatar()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/default_avatar.png", UriKind.Absolute);
                var bmp = new BitmapImage(uri);
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return new BitmapImage();
            }
        }

        private void LoadUserData()
        {
            if (_currentUser == null || _currentUser.UserId <= 0)
            {
                ShowError("Не удалось определить пользователя.");
                return;
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string query = "SELECT Username, Email, ProfileImage FROM Users WHERE UserId = @UserId";

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", _currentUser.UserId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                _currentUser.Username = reader["Username"].ToString();
                                _currentUser.Email = reader["Email"].ToString();
                                _currentUser.ProfileImage = reader["ProfileImage"] as byte[]; 
                            }
                        }
                    }
                }

                UsernameBox.Text = _currentUser.Username;
                EmailBox.Text = _currentUser.Email;

                ProfileImage.Source = BytesToBitmapImage(_currentUser.ProfileImage) ?? DefaultAvatar();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка загрузки данных: {ex.ToString()}");
            }
        }

        private void ChangePhoto_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите фото профиля",
                Filter = "Изображения (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    byte[] photoBytes = File.ReadAllBytes(dialog.FileName);

                    var bmp = BytesToBitmapImage(photoBytes);
                    if (bmp != null)
                    {
                        ProfileImage.Source = bmp;
                        _newPhotoBytes = photoBytes;
                    }
                    else
                    {
                        ShowError("Не удалось загрузить изображение. Проверьте файл.");
                    }
                }
                catch (Exception ex)
                {
                    ShowError($"Ошибка при загрузке изображения: {ex.ToString()}");
                }
            }
        }


        private BitmapImage BytesToBitmapImage(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            try
            {
                using (var ms = new MemoryStream(bytes))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка конвертации изображения: {ex.ToString()}");
                return null;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameBox.Text.Trim();
            string email = EmailBox.Text.Trim();
            string password = PasswordBox.Password.Trim();
            string confirmPassword = ConfirmPasswordBox.Password.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError("Имя пользователя не может быть пустым.");
                return;
            }

            if (!IsValidEmail(email))
            {
                ShowError("Введите корректный email.");
                return;
            }

            if (!string.IsNullOrEmpty(password))
            {
                if (password.Length < 5)
                {
                    ShowError("Пароль должен быть не менее 5 символов.");
                    return;
                }

                if (password != confirmPassword)
                {
                    ShowError("Пароли не совпадают.");
                    return;
                }
            }

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string query;
                    if (!string.IsNullOrEmpty(password))
                    {
                        string hash = BCrypt.Net.BCrypt.HashPassword(password);
                        query = "UPDATE Users SET Username=@Username, Email=@Email, PasswordHash=@Hash WHERE UserId=@UserId";
                        using (var cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@Username", username);
                            cmd.Parameters.AddWithValue("@Email", email);
                            cmd.Parameters.AddWithValue("@Hash", hash);
                            cmd.Parameters.AddWithValue("@UserId", _currentUser.UserId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        query = "UPDATE Users SET Username=@Username, Email=@Email WHERE UserId=@UserId";
                        using (var cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@Username", username);
                            cmd.Parameters.AddWithValue("@Email", email);
                            cmd.Parameters.AddWithValue("@UserId", _currentUser.UserId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    if (_newPhotoBytes != null)
                    {
                        string photoQuery = "UPDATE Users SET ProfileImage=@Image WHERE UserId=@UserId";
                        using (var cmd = new SqlCommand(photoQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@Image", _newPhotoBytes); 
                            cmd.Parameters.AddWithValue("@UserId", _currentUser.UserId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                _currentUser.Username = username;
                _currentUser.Email = email;
                if (_newPhotoBytes != null)
                    _currentUser.ProfileImage = _newPhotoBytes;

                ShowSuccess("Изменения успешно сохранены!");
                AnimateAndClose();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при сохранении: {ex.ToString()}");
            }
        }

        private void AnimateAndClose()
        {
            var sb = new Storyboard();

            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
            Storyboard.SetTarget(fadeOut, this);
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(Window.OpacityProperty));

            sb.Children.Add(fadeOut);
            sb.Completed += (s, e) => Close();
            sb.Begin();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private bool IsValidEmail(string email)
        {
            return !string.IsNullOrEmpty(email) && Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }

        private void ShowError(string message) =>
            MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

        private void ShowSuccess(string message) =>
            MessageBox.Show(message, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
