using System;
using System.Linq;
using System.Windows;
using TrainingProcessMonitor.Wpf.ViewModels;

namespace TrainingProcessMonitor.Wpf
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var viewModel = new MainViewModel();
            DataContext = viewModel;

            if (Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, "--autoTest", StringComparison.OrdinalIgnoreCase)))
            {
                Loaded += (sender, args) => viewModel.RunAutoTest();
            }
        }
    }
}
