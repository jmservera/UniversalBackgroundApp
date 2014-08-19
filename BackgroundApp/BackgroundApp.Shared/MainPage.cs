namespace BackgroundApp
{
    using Windows.UI.Xaml;
    public partial class MainPage
    {
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var vm = mainGrid.DataContext as BackgroundAppViewModel;
            if (vm!=null && !vm.IsTaskRegistered)
            {
                vm.IsTaskRegistered = true;
            }
        }
    }
}
