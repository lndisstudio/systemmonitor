using Avalonia.Controls;
using Avalonia.Interactivity;
using SystemMonitor.ViewModels;

namespace SystemMonitor.Views
{
    public partial class LogWindow : Window
    {
        public LogWindow()
        {
            InitializeComponent();
        }

        public LogWindow(MainWindowViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void CloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
