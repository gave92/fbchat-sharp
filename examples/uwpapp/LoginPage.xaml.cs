using fbchat_sharp.API;
using System;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace uwpapp
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
            var dialog = new InputBox();
            dialog.Title = "2FA code";
            dialog.PlaceholderText = "Insert 2FA code...";
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var text = dialog.Text;
                return text;
            }
            return null;
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
                    this.Frame.Navigate(typeof(MainPage));
                }
            }
        }
    }
}
