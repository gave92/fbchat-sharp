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
            try
            {
                await client.TryLogin();
                Frame.Navigate(new Uri("MainPage.xaml", UriKind.RelativeOrAbsolute));
            }
            catch
            {
                Frame.Navigate(new Uri("LoginPage.xaml", UriKind.RelativeOrAbsolute));
            }
            finally
            {
                Progress.IsEnabled = false;
                Progress.Visibility = Visibility.Collapsed;
            }
        }
    }
}
