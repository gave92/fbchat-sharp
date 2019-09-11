using fbchat_sharp.API;
using System.Windows;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace uwpapp.Helpers
{
    public class MessageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate UserMessageTemplate { get; set; }
        public DataTemplate OwnMessageTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            FB_Message message = item as FB_Message;
            if (message.is_from_me)
            {
                return OwnMessageTemplate;
            }
            else
            {
                return UserMessageTemplate;
            }
        }
    }
}
