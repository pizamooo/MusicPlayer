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
