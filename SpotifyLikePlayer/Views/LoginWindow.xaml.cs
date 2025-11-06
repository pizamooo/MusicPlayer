using SpotifyLikePlayer.Services;
using SpotifyLikePlayer.ViewModels;
using SpotifyLikePlayer.Views;
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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SpotifyLikePlayer
{
    /// <summary>
    /// Логика взаимодействия для LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        private bool _isClosingAnimated = false;
        private MainViewModel _vm = new MainViewModel();
        public LoginWindow()
        {
            InitializeComponent();
            App.CurrentLoginWindow = this;
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (App.CurrentLoginWindow == this)
                App.CurrentLoginWindow = null;

            base.OnClosed(e);
        }
        private void Login_Click(object sender, RoutedEventArgs e)
        {
            string username = UsernameTxt.Text;
            string password = PasswordTxt.Password;

            if (string.IsNullOrEmpty(password) || password.Length < 5)
            {
                ErrorMessage.Text = "Пароль должен содержать минимум 5 символов.";
                return;
            }

            _vm.Login(username, password);
            if (_vm.CurrentUser != null)
            {
                new MainWindow(_vm).Show();
                this.Close();
            }
            else
            {
                ErrorMessage.Text = "Неверный логин или пароль.";
            }
        }

        private void OpenRegister_Click(object sender, RoutedEventArgs e)
        {
            RegisterWindow registerWindow = new RegisterWindow(_vm);
            bool? dialogResult = registerWindow.ShowDialog();

            if (dialogResult == true)
            {
                new MainWindow(_vm).Show();
                this.Close();
            }
        }

        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            new ForgotPasswordWindow(_vm).ShowDialog();
        }

        private void ClearErrorMessage(object sender, RoutedEventArgs e)
        {
            ErrorMessage.Text = "";
        }
    }
}
