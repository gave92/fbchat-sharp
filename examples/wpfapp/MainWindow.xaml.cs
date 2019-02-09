using System;
using System.Windows;

namespace wpfapp
{
    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var client = ((App)Application.Current).client;
            var logged_in = await client.TryLogin();

            Progress.IsEnabled = false;
            Progress.Visibility = Visibility.Collapsed;

            if (!logged_in)
            {
                Frame.Navigate(new Uri("LoginPage.xaml", UriKind.RelativeOrAbsolute));                
            }
            else
            {
                Frame.Navigate(new Uri("MainPage.xaml", UriKind.RelativeOrAbsolute));
            }
        }
    }
}
