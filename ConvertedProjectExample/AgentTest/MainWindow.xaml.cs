using AgentTest.ViewModels;
// using DllTest; // REMOVED: DllTest namespace doesn't exist
using System.Windows;

namespace AgentTest
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = App.ViewModel;
        }
    }
}






