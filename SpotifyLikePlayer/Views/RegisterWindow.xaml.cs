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

using System.Text.RegularExpressions;
using SpotifyLikePlayer.ViewModels;
using System.Windows.Media.Animation;

namespace SpotifyLikePlayer.Views
{
    /// <summary>
    /// Логика взаимодействия для RegisterWindow.xaml
    /// </summary>
    public partial class RegisterWindow : Window
    {
        private bool _isClosingAnimated = false;
        private MainViewModel _vm;
        public RegisterWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isClosingAnimated)
            {
                return;
            }

            e.Cancel = true;

            _isClosingAnimated = true;

            var storyboard = new Storyboard();

            var scaleX = new DoubleAnimation(1.0, 0.9, TimeSpan.FromMilliseconds(150));
            var scaleY = new DoubleAnimation(1.0, 0.9, TimeSpan.FromMilliseconds(150));
            var fade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(150));

            Storyboard.SetTarget(scaleX, MainContainer);
            Storyboard.SetTarget(scaleY, MainContainer);
            Storyboard.SetTarget(fade, this);

            Storyboard.SetTargetProperty(scaleX, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
            Storyboard.SetTargetProperty(scaleY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
            Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));

            storyboard.Children.Add(scaleX);
            storyboard.Children.Add(scaleY);
            storyboard.Children.Add(fade);

            storyboard.Completed += (s, args) =>
            {
                this.Close();
            };

            storyboard.Begin();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (App.CurrentLoginWindow != null)
            {
                App.CurrentLoginWindow.WindowState = WindowState.Normal;
                App.CurrentLoginWindow.Activate();
            }
            else
            {
                new LoginWindow().Show();
            }
        }

        private void GoBackToLogin_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {

            if (string.IsNullOrEmpty(UsernameTxt.Text) || string.IsNullOrEmpty(PasswordTxt.Password) || string.IsNullOrEmpty(EmailTxt.Text))
            {
                ErrorMessage.Text = "Заполните все поля."; return;
            }

            string username = UsernameTxt.Text;
            string email = EmailTxt.Text;
            string password = PasswordTxt.Password;
            string confirmPassword = ConfirmPasswordTxt.Password;

            // Проверки
            if (password.Length < 5)
            {
                ErrorMessage.Text = "Пароль должен быть минимум 5 символов."; return;
            }
            if (password != confirmPassword)
            {
                ErrorMessage.Text = "Пароли не совпадают."; return;
            }
            if (!IsValidEmail(email))
            {
                ErrorMessage.Text = "Неверный формат email."; return;
            }

            bool success = _vm.Register(username, password, email);
            if (success)
            {
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                ErrorMessage.Text = "Пользователь с таким именем или email уже существует.";
            }
        }

        private bool IsValidEmail(string email)
        {
            var regex = new Regex(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$");
            return regex.IsMatch(email);
        }

        private void ClearErrorMessage(object sender, RoutedEventArgs e)
        {
            ErrorMessage.Text = "";
        }
    }
}
