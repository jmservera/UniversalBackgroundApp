using System;
using System.Collections.Generic;
using System.Text;

namespace BackgroundApp
{
    public partial class MainPage
    {
        bool registered = false;
        protected async override void OnGotFocus(Windows.UI.Xaml.RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            if (!registered)
            {
                registered = true;
                await BackgroundTasks.BackgroundDemoTask.RegisterTaskAsync();
            }
        }
    }
}
