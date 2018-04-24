public class App : Application
{
    public StackLayoutExample()
    {
        Padding = new Thickness(20);
        var red = new Label
        {
            Text = "Stop",
            BackgroundColor = Color.Red,
            FontSize = 20
        };
        var yellow = new Label
        {
            Text = "Slow down",
            BackgroundColor = Color.Yellow,
            FontSize = 20
        };
        var green = new Label
        {
            Text = "Go",
            BackgroundColor = Color.Green,
            FontSize = 20
        };

        Content = new StackLayout
        {
            Spacing = 10,
            Children = { red, yellow, green }
        };

    }
}