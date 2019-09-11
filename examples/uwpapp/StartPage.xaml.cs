using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

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
            var logged_in = await client.TryLogin();

            Progress.IsEnabled = false;
            Progress.Visibility = Visibility.Collapsed;

            if (!logged_in)
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
