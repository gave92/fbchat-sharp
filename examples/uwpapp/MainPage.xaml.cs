using fbchat_sharp.API;
using Microsoft.Toolkit.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using uwpapp.Helpers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using static uwpapp.FBClient_Uwp;

namespace uwpapp
{
    /// <summary>
    /// Logica di interazione per MainPage.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        private static SemaphoreSlim SlowStuffSemaphore = new SemaphoreSlim(1, 1);

        public FBClient_Uwp Client { get; private set; }
        public ObservableCollection<FB_Thread> Threads { get; private set; }
        public ObservableObject<FB_Thread> SelectedThread { get; private set; }
        public ObservableObject<FB_User> Profile { get; private set; }
        public IncrementalLoadingCollection<MessageSource, FB_Message> Messages { get; private set; }        

        public MainPage()
        {
            InitializeComponent();
            Client = ((App)Application.Current).client;
            Profile = new ObservableObject<FB_User>();
            SelectedThread = new ObservableObject<FB_Thread>();
            Threads = new ObservableCollection<FB_Thread>();
            Messages = new IncrementalLoadingCollection<MessageSource, FB_Message>(new MessageSource(this), invertedList: true);
            this.DataContext = this;
            this.Loaded += MainPage_Loaded;
            this.Unloaded += MainPage_Unloaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Profile.Value = await Client.fetchProfile();
            await UpdateThreadList();
            Client.UpdateEvent += Client_UpdateEvent;
            await Client.StartListening();

            var scrollViewer = GetScrollViewer(MessageList) as ScrollViewer;
            scrollViewer.ViewChanged += ScrollViewer_ViewChanged;
        }

        private async void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            var scrollViewer = sender as ScrollViewer;
            if (!e.IsIntermediate && scrollViewer.VerticalOffset < 10)
                await Messages.LoadMoreItemsAsync(20);
        }

        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Client.UpdateEvent -= Client_UpdateEvent;
            Client.StopListening();
        }

        private async void Client_UpdateEvent(object sender, UpdateEventArgs e)
        {
            if (e.EventType == UpdateStatus.NEW_MESSAGE)
            {
                var msg = e.Payload as FB_Message;
                if (msg.thread_id == SelectedThread.Value.uid)
                {
                    msg.is_from_me = msg.author == Client.GetUserUid();
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => Messages.Add(msg));
                }
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            await Client.DoLogout();
            this.Frame.Navigate(typeof(LoginPage));
        }

        private async void ThreadListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            var thread = (FB_Thread)e.AddedItems[0];
            SelectedThread.Value = thread;
            await Messages.RefreshAsync();
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var text = SendText.Text;
            if (SelectedThread.Value == null || string.IsNullOrWhiteSpace(text)) return;
            await Client.sendMessage(text, SelectedThread.Value.uid, SelectedThread.Value.type);
            SendText.Text = "";
        }

        private async Task<List<FB_Message>> UpdateMessageList(int pageSize = 20)
        {
            if (SelectedThread.Value == null) return null;
            if (SlowStuffSemaphore.CurrentCount == 0) return null;
            await SlowStuffSemaphore.WaitAsync();
            try
            {
                var messages = await Client.fetchThreadMessages(SelectedThread.Value.uid, pageSize, Messages.FirstOrDefault()?.timestamp);
                if (messages.Any() && Messages.Any() && messages.First().uid == Messages.First().uid) messages.RemoveAt(0);                
                foreach (var msg in messages)
                {
                    msg.is_from_me = msg.author == Client.GetUserUid();
                }
                return messages;
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
                var threads = await Client.fetchThreadList(20, ThreadLocation.INBOX, Threads.LastOrDefault()?.last_message_timestamp);
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

        public class MessageSource : IIncrementalSource<FB_Message>
        {
            private readonly List<FB_Message> messages;
            private readonly MainPage page;

            public MessageSource(MainPage page)
            {
                // Creates an example collection.
                this.messages = new List<FB_Message>();
                this.page = page;
            }

            public async Task<IEnumerable<FB_Message>> GetPagedItemsAsync(int pageIndex, int pageSize, CancellationToken cancellationToken)
            {
                // Gets items from the collection according to pageIndex and pageSize parameters.
                return await this.page.UpdateMessageList(pageSize: pageSize);
            }
        }
    }
}
