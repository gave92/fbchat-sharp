using examples;
using fbchat_sharp.API;
using System.Windows;

namespace wpfapp
{
    /// <summary>
    /// Logica di interazione per App.xaml
    /// </summary>
    public partial class App : Application
    {
        // FBClient
        private MessengerClient _client;

        public MessengerClient client
        {
            get
            {
                if (_client == null)
                    _client = new FBClient_Wpf();
                return _client;
            }
        }
    }
}
