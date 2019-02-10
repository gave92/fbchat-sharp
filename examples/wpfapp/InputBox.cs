using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace wpfapp
{
    public class InputBox
    {
        Window Box = new Window();//window for the inputbox
        FontFamily font = new FontFamily("Tahoma");//font for the whole inputbox
        int FontSize = 16;//fontsize for the input
        StackPanel sp1 = new StackPanel();// items container
        string title = "";//title as heading
        string boxcontent;//title
        string defaulttext = "";//default textbox content
        string okbuttontext = "OK";//Ok button content
        Brush BoxBackgroundColor = Brushes.White;// Window Background
        Brush InputBackgroundColor = Brushes.Transparent;// Textbox Background
        bool clicked = false;
        TextBlock content = new TextBlock();
        TextBox input = new TextBox();
        Button ok = new Button();
        bool inputreset = false;

        public InputBox(string content)
        {
            boxcontent = content;
            windowdef();
        }

        public InputBox(string content, string Htitle, string DefaultText)
        {
            boxcontent = content;
            title = Htitle;
            defaulttext = DefaultText;
            windowdef();
        }

        public InputBox(string content, string Htitle)
        {
            boxcontent = content;
            title = Htitle;
            windowdef();
        }

        private void windowdef()// window building - check only for window size
        {
            sp1.HorizontalAlignment = HorizontalAlignment.Center;
            sp1.VerticalAlignment = VerticalAlignment.Center;
            sp1.Width = 200;// Box Width

            Box.Height = 120;// Box Height
            Box.MinHeight = 120;// Box Height
            Box.Width = 300;// Box Width
            Box.MinWidth = 300;// Box Width
            Box.Background = BoxBackgroundColor;
            Box.Title = title;
            Box.Content = sp1;
            Box.Closing += Box_Closing;

            if (!string.IsNullOrWhiteSpace(boxcontent))
            {
                content.TextWrapping = TextWrapping.Wrap;
                content.Background = null;
                content.HorizontalAlignment = HorizontalAlignment.Left;
                content.Text = boxcontent;
                content.FontSize = FontSize;
                sp1.Children.Add(content);
            }            

            input.Background = InputBackgroundColor;
            input.FontSize = FontSize;
            input.HorizontalAlignment = HorizontalAlignment.Stretch;
            input.Text = defaulttext;
            input.MouseEnter += input_MouseDown;
            sp1.Children.Add(input);

            ok.Click += ok_Click;
            ok.FontSize = FontSize;
            ok.Content = okbuttontext;
            ok.Width = 200;
            ok.HorizontalAlignment = HorizontalAlignment.Center;
            sp1.Children.Add(ok);
        }

        void Box_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!clicked)
                e.Cancel = true;
        }

        private void input_MouseDown(object sender, MouseEventArgs e)
        {
            if ((sender as TextBox).Text == defaulttext && inputreset == false)
            {
                (sender as TextBox).Text = null;
                inputreset = true;
            }
        }

        void ok_Click(object sender, RoutedEventArgs e)
        {
            clicked = true;
            if (input.Text == defaulttext || input.Text == "")
            {
                //MessageBox.Show(errormessage, errortitle);
            }                
            else
            {
                Box.Close();
            }
            clicked = false;
        }

        public string ShowDialog()
        {
            Box.ShowDialog();
            return input.Text;
        }
    }
}
