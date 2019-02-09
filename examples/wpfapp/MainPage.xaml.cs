using fbchat_sharp.API;
using System;
using System.Windows;
using System.Windows.Controls;

namespace wpfapp
{
    /// <summary>
    /// Logica di interazione per MainPage.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        public MessengerClient Client { get; private set; }
        public FB_User Profile { get; private set; }

        public MainPage()
        {
            InitializeComponent();            
            Client = ((App)Application.Current).client;
            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Profile = await Client.FetchProfile();
            this.DataContext = this;
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            await Client.DoLogout();
            NavigationService.Navigate(new Uri("LoginPage.xaml", UriKind.RelativeOrAbsolute));
        }
    }
}
