using System.Configuration;
using System.Data;
using System.Windows;
using RailSDK;
using WorkflowDemo.ViewModels;
using WorkflowDemo.Services;

namespace WorkflowDemo;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private UIHighlightRouter? _highlightRouter;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Start RailEngine Named Pipe server
        RailEngine.Ignite(this);

        // Set up UI highlighting for demo mode
        var mainWindow = new MainWindow();
        var mainViewModel = new MainViewModel();
        mainWindow.DataContext = mainViewModel;

        _highlightRouter = new UIHighlightRouter(mainViewModel);

        // Subscribe to function call events for UI highlighting
        //RailEngine.OnFunctionCalling += async (evt) =>
        //{
        //    await _highlightRouter.HandleFunctionCallAsync(evt);
        //};

        mainWindow.Show();
    }
}





