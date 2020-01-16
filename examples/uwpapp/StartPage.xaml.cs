using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// Il modello di elemento Pagina vuota è documentato all'indirizzo https://go.microsoft.com/fwlink/?LinkId=234238

namespace uwpapp
{
    /// <summary>
    /// Pagina vuota che può essere usata autonomamente oppure per l'esplorazione all'interno di un frame.
    /// </summary>
    public sealed partial class StartPage : Page
    {
        public StartPage()
        {
            this.InitializeComponent();
            this.Loaded += StartPage_Loaded;
        }

        private async void StartPage_Loaded(object sender, RoutedEventArgs e)
        {
            var client = ((App)Application.Current).client;
            var session = await client.TryLogin();

            Progress.IsEnabled = false;
            Progress.Visibility = Visibility.Collapsed;

            if (session != null)
            {
                ContentFrame.Navigate(typeof(LoginPage));
            }
            else
            {
                ContentFrame.Navigate(typeof(MainPage));
            }
        }
    }
}
