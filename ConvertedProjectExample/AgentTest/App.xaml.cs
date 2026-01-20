using AgentTest.ViewModels;
using RailSDK;
using System.Windows;

namespace AgentTest
{
    public partial class App : Application
    {
        public static MainViewModel ViewModel { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize ViewModel
            ViewModel = new MainViewModel();

            // Initialize RailEngine with ViewModel
            // All public methods of MainViewModel will be callable by LLM
            RailEngine.Ignite(ViewModel);
        }
    }
}







