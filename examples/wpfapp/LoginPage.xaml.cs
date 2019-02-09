using fbchat_sharp.API;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace wpfapp
{
    /// <summary>
    /// Logica di interazione per LooginPage.xaml
    /// </summary>
    public partial class LoginPage : Page
    {
        public MessengerClient Client { get; private set; }

        public LoginPage()
        {
            InitializeComponent();
            Client = ((App)Application.Current).client;
            Client.Set2FACallback(On2FACallback);
        }

        private async Task<string> On2FACallback()
        {
            return new InputBox("2FA code", "2FA code", "Insert 2FA code...").ShowDialog();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var email = Email.Text;
            var password = Password.Password;
            if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
            {
                // Login with username and password
                var logged_in = await Client.DoLogin(email, password);

                // Check login was successful
                if (logged_in)
                {
                    NavigationService.Navigate(new Uri("MainPage.xaml", UriKind.RelativeOrAbsolute));
                }
            }
        }        
    }
}
