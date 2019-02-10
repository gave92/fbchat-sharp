using fbchat_sharp.API;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using wpfapp.Helpers;

namespace wpfapp
{
    /// <summary>
    /// Logica di interazione per MainPage.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        public MessengerClient Client { get; private set; }
        public ObservableCollection<FB_Thread> Threads { get; private set; }
        public ObservableObject<FB_Thread> SelectedThread { get; private set; }
        public ObservableObject<FB_User> Profile { get; private set; }
        public ObservableCollection<FB_Message> Messages { get; private set; }

        public MainPage()
        {
            InitializeComponent();
            Client = ((App)Application.Current).client;
            Profile = new ObservableObject<FB_User>();
            SelectedThread = new ObservableObject<FB_Thread>();
            Threads = new ObservableCollection<FB_Thread>();
            Messages = new ObservableCollection<FB_Message>();
            this.DataContext = this;
            this.Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Profile.Value = await Client.FetchProfile();
            var threads = await Client.FetchThreadList();
            foreach (var thread in threads)
            {
                Threads.Add(thread);
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            await Client.DoLogout();
            NavigationService.Navigate(new Uri("LoginPage.xaml", UriKind.RelativeOrAbsolute));
        }

        private async void ThreadListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            var thread = (FB_Thread)e.AddedItems[0];
            SelectedThread.Value = thread;
            await UpdateMessageList();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var text = SendText.Text;
            if (string.IsNullOrWhiteSpace(text)) return;
            await Client.SendMessage(text, SelectedThread.Value.uid, SelectedThread.Value.type);
            SendText.Clear();
            await UpdateMessageList();
        }

        private async Task UpdateMessageList()
        {
            var messages = await Client.FetchThreadMessages(SelectedThread.Value.uid);
            Messages.Clear();
            for (var i = messages.Count - 1; i >= 0; i--)
            {
                messages[i].is_from_me = messages[i].author == Client.GetUserUid();
                Messages.Add(messages[i]);
            }
            MessageList.ScrollIntoView(Messages.Last());
        }
    }
}
