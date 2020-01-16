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
            this.Loaded += LoginPage_Loaded;
            this.Unloaded += LoginPage_Unloaded;
        }

        private void LoginPage_Loaded(object sender, RoutedEventArgs e)
        {
            Client.Set2FACallback(On2FACallback);
        }

        private void LoginPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Client.Set2FACallback(null);
        }

        private async Task<string> On2FACallback()
        {
            await Task.Yield();
            return new InputBox("2FA code", "2FA code", "Insert 2FA code...").ShowDialog();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var email = Email.Text;
            var password = Password.Password;
            if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
            {
                // Login with username and password
                var session = await Client.DoLogin(email, password);

                // Check login was successful
                if (session != null)
                {
                    NavigationService.Navigate(new Uri("MainPage.xaml", UriKind.RelativeOrAbsolute));
                }
            }
        }
    }
}
