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
                try
                {
                    // Login with username and password
                    await Client.DoLogin(email, password);
                    NavigationService.Navigate(new Uri("MainPage.xaml", UriKind.RelativeOrAbsolute));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not login: {ex.Message}",
                                    "Login error.",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                }
            }
        }
    }
}
