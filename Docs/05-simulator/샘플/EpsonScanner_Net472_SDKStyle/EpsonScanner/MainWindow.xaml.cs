using System.Windows;
using EpsonScanner.ViewModels;

namespace EpsonScanner
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
