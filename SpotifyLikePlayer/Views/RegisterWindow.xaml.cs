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

namespace SpotifyLikePlayer.Views
{
    /// <summary>
    /// Логика взаимодействия для RegisterWindow.xaml
    /// </summary>
    public partial class RegisterWindow : Window
    {
        private MainViewModel _vm;
        public RegisterWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {

            if (string.IsNullOrEmpty(UsernameTxt.Text) || string.IsNullOrEmpty(PasswordTxt.Password) || string.IsNullOrEmpty(EmailTxt.Text))
            {
                MessageBox.Show("Заполните все поля.");
                return;
            }

            string username = UsernameTxt.Text;
            string email = EmailTxt.Text;
            string password = PasswordTxt.Password;
            string confirmPassword = ConfirmPasswordTxt.Password;

            // Проверки
            if (password.Length < 5)
            {
                MessageBox.Show("Password должен быть минимум 5 символов.");
                return;
            }
            if (password != confirmPassword)
            {
                MessageBox.Show("Пароли не совпадают.");
                return;
            }
            if (!IsValidEmail(email))
            {
                MessageBox.Show("Неверный формат email.");
                return;
            }

            bool success = _vm.Register(username, password, email);
            if (success)
            {
                this.DialogResult = true;
                this.Close();
            }
        }

        private bool IsValidEmail(string email)
        {
            // Простой regex для email
            var regex = new Regex(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$");
            return regex.IsMatch(email);
        }
    }
}
