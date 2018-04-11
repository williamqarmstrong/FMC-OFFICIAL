using System;

using Xamarin.Forms;

namespace FMC
{
    public class Connect : ContentPage
    {
        public Connect()
        {
            Content = new StackLayout
            {
                Children = {
                    new Label { Text = "Hello ContentPage" }
                }
            };
        }
    }
}

