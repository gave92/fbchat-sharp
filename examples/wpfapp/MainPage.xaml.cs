using fbchat_sharp.API;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using wpfapp.Helpers;

namespace wpfapp
{
    /// <summary>
    /// Logica di interazione per MainPage.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        private static SemaphoreSlim SlowStuffSemaphore = new SemaphoreSlim(1, 1);

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
            this.Unloaded += MainPage_Unloaded;
        }

        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Client.UpdateEvent -= Client_UpdateEvent;
            Client.StopListening();
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Profile.Value = await Client.FetchProfile();
            await UpdateThreadList();
            Client.UpdateEvent += Client_UpdateEvent;
            Client.StartListening();
        }

        private void Client_UpdateEvent(object sender, UpdateEventArgs e)
        {
            if (e.EventType == UpdateStatus.NEW_MESSAGE)
            {
                var msg = e.Payload as FB_Message;
                if (msg.thread_id == SelectedThread.Value.uid)
                {
                    msg.is_from_me = msg.author == Client.GetUserUid();
                    Dispatcher.Invoke(() => Messages.Add(msg));
                }
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
            await UpdateMessageList(clear: true);
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var text = SendText.Text;
            if (SelectedThread.Value == null || string.IsNullOrWhiteSpace(text)) return;
            await Client.SendMessage(text, SelectedThread.Value.uid, SelectedThread.Value.type);
            SendText.Clear();
        }

        private async Task UpdateMessageList(bool clear)
        {
            if (SlowStuffSemaphore.CurrentCount == 0) return;
            await SlowStuffSemaphore.WaitAsync();
            try
            {
                if (clear) Messages.Clear();
                var messages = await Client.FetchThreadMessages(SelectedThread.Value.uid, 20, Messages.FirstOrDefault()?.timestamp);
                if (messages.Any() && Messages.Any() && messages.First().uid == Messages.First().uid) messages.RemoveAt(0);
                ScrollViewer scrollViewer = GetScrollViewer(MessageList) as ScrollViewer;
                var prev_height = scrollViewer.ExtentHeight;
                foreach (var msg in messages)
                {
                    msg.is_from_me = msg.author == Client.GetUserUid();
                    Messages.Insert(0, msg);
                }
                if (clear)
                {
                    await Task.Delay(200).ContinueWith(_ => Dispatcher.Invoke(() => scrollViewer.ScrollToBottom()));
                }
                else
                {
                    await Task.Delay(200).ContinueWith(_ => Dispatcher.Invoke(() => scrollViewer.ScrollToVerticalOffset(scrollViewer.ExtentHeight - prev_height)));
                }
            }
            finally
            {
                SlowStuffSemaphore.Release();
            }
        }

        private async Task UpdateThreadList()
        {
            if (SlowStuffSemaphore.CurrentCount == 0) return;
            await SlowStuffSemaphore.WaitAsync();
            try
            {
                var threads = await Client.FetchThreadList(20, ThreadLocation.INBOX, Threads.LastOrDefault()?.last_message_timestamp);
                threads.RemoveAll(x => Threads.Any(y => x.uid == y.uid));
                foreach (var thread in threads)
                {
                    Threads.Add(thread);
                }
            }
            finally
            {
                SlowStuffSemaphore.Release();
            }
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            var menuButton = sender as FrameworkElement;
            if (menuButton != null)
            {
                menuButton.ContextMenu.IsOpen = true;
            }
        }

        private async void MessageList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange < 0)
            {
                if (e.VerticalOffset == 0)
                {
                    // Top of the list
                    await UpdateMessageList(clear: false);
                }
            }
        }

        private async void ThreadList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange > 0)
            {
                // Bottom of the list
                if (e.VerticalOffset + e.ViewportHeight == e.ExtentHeight)
                {
                    await UpdateThreadList();
                }
            }
        }

        public static DependencyObject GetScrollViewer(DependencyObject o)
        {
            if (o is ScrollViewer) return o;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                var child = VisualTreeHelper.GetChild(o, i);
                var result = GetScrollViewer(child);
                if (result == null) continue;
                else return result;
            }

            return null;
        }
    }
}
