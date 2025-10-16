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
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SpotifyLikePlayer
{
    /// <summary>
    /// Логика взаимодействия для LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        private MainViewModel _vm = new MainViewModel();
        public LoginWindow()
        {
            InitializeComponent();
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
            bool? dialogResult = registerWindow.ShowDialog(); // Открываем модально

            if (dialogResult == true) // Если регистрация успешна
            {
                new MainWindow(_vm).Show(); // Открываем основное окно
                this.Close(); // Закрываем окно логина
            }
        }

        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            new ForgotPasswordWindow(_vm).ShowDialog(); // Открываем окно сброса пароля
        }

        private void ClearErrorMessage(object sender, RoutedEventArgs e)
        {
            ErrorMessage.Text = "";  // Очищаем сообщение при вводе
        }
    }
}
