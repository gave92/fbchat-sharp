using fbchat_sharp.API;
using System.Windows;
using System.Windows.Controls;

namespace wpfapp.Helpers
{
    public class MessageTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            FrameworkElement elemnt = container as FrameworkElement;
            FB_Message message = item as FB_Message;
            if (message.is_from_me)
            {
                return elemnt.FindResource("OwnMessageTemplate") as DataTemplate;
            }
            else
            {
                return elemnt.FindResource("UserMessageTemplate") as DataTemplate;
            }
        }
    }
}
