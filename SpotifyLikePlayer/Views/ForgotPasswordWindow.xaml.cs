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
    /// Логика взаимодействия для ForgotPasswordWindow.xaml
    /// </summary>
    public partial class ForgotPasswordWindow : Window
    {
        private bool _isClosingAnimated = false;
        private MainViewModel _vm;
        public ForgotPasswordWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isClosingAnimated)
                return;

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

            storyboard.Completed += (s, args) => this.Close();
            storyboard.Begin();
        }

        private void GoBackToLogin_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
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

        private void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTxt.Text;
            string newPassword = NewPasswordTxt.Password;
            string confirmPassword = ConfirmPasswordTxt.Password;

            // Проверки
            if (!IsValidEmail(email))
            {
                ErrorMessage.Text = "Неверный формат email."; return;
            }
            if (newPassword.Length < 5)
            {
                ErrorMessage.Text = "Новый пароль должен быть минимум 5 символов."; return;
            }
            if (newPassword != confirmPassword)
            {
                ErrorMessage.Text = "Пароли не совпадают."; return;
            }

            bool success = _vm.ResetPassword(email, newPassword);
            if (success)
            {
                ErrorMessage.Text = "Пароль успешно сброшен! Теперь войдите.";
                this.Close();
            }
            else
            {
                ErrorMessage.Text = "Пользователь с таким email не найден.";
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
