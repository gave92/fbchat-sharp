using fbchat_sharp.API;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace wpfapp.Xaml
{
    /// <summary>
    /// Logica di interazione per MessageControl.xaml
    /// </summary>
    public partial class MessageControl : UserControl
    {
        public MessageControl()
        {
            InitializeComponent();
            this.DataContextChanged += MessageControl_DataContextChanged;
        }

        private void MessageControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue == null) return;
            var Message = e.NewValue as FB_Message;
            if (Message == null) return;

            foreach (var image in Message.attachments.Where(x => x is FB_ImageAttachment))
            {
                this.Content.Children.Add(new ContentControl() { ContentTemplate = (DataTemplate)this.Resources["ImageMessageTemplate"], Content = image });
            }            
            if (Message.sticker != null)
            {
                this.Content.Children.Add(new ContentControl() { ContentTemplate = (DataTemplate)this.Resources["StickerMessageTemplate"], Content = Message.sticker });
            }
            if (Message.text.Any())
            {
                if (Message.is_from_me)
                {
                    this.Content.Children.Add(new ContentControl() { ContentTemplate = (DataTemplate)this.Resources["OwnTextMessageTemplate"], Content = Message });
                }
                else
                {
                    this.Content.Children.Add(new ContentControl() { ContentTemplate = (DataTemplate)this.Resources["UserTextMessageTemplate"], Content = Message });
                }                
            }
        }
    }
}
