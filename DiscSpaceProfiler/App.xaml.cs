using DiscSpaceProfiler.ViewModels;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace DiscSpaceProfiler
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        void App_Startup(object sender, StartupEventArgs e)
        {
            var viewModel = new MainWindowViewModel();
            MainWindow window = new MainWindow(viewModel);
            window.Show();
        }
    }
}
