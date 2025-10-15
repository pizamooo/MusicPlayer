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
    /// Логика взаимодействия для ForgotPasswordWindow.xaml
    /// </summary>
    public partial class ForgotPasswordWindow : Window
    {
        private MainViewModel _vm;
        public ForgotPasswordWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
        }

        private void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTxt.Text;
            string newPassword = NewPasswordTxt.Password;
            string confirmPassword = ConfirmPasswordTxt.Password;

            // Проверки
            if (!IsValidEmail(email))
            {
                MessageBox.Show("Неверный формат email.");
                return;
            }
            if (newPassword.Length < 5)
            {
                MessageBox.Show("Новый пароль должен быть минимум 5 символов.");
                return;
            }
            if (newPassword != confirmPassword)
            {
                MessageBox.Show("Пароли не совпадают.");
                return;
            }

            bool success = _vm.ResetPassword(email, newPassword);
            if (success)
            {
                MessageBox.Show("Пароль успешно сброшен! Теперь войдите.");
                this.Close();
            }
            else
            {
                MessageBox.Show("Пользователь с таким email не найден.");
            }
        }

        private bool IsValidEmail(string email)
        {
            var regex = new Regex(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$");
            return regex.IsMatch(email);
        }
    }
}
