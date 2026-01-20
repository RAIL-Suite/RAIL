using System.Windows.Input;
using WpfRagApp.Services;
using WpfRagApp.Services.Host;

namespace WpfRagApp.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private readonly LLMService _llmService;
        
        private ViewModelBase _currentView;
        private HomeViewModel _homeViewModel;
        private SettingsViewModel _settingsViewModel;
        
        /// <summary>
        /// Gets the HomeViewModel for use by MiniPromptWindow.
        /// </summary>
        public HomeViewModel HomeViewModel => _homeViewModel;

        private HostService _host;
        public MainViewModel()
        {
            // Create SettingsService first (needed by AssetService for configurable path)
            _settingsService = new SettingsService();
            
            // Create AssetService with SettingsService for dynamic path configuration
            var assetService = new AssetService(_settingsService);
            
            // Create HostService with AssetService for manifest lookup
            _host = new HostService(assetService);
            _ = Task.Run(() => _host.Start());

            _llmService = new LLMService(_settingsService, _host);

            _homeViewModel = new HomeViewModel(_llmService, _settingsService);
            _settingsViewModel = new SettingsViewModel(_settingsService);

            _currentView = _homeViewModel;

            NavigateHomeCommand = new RelayCommand(_ => CurrentView = _homeViewModel);
            NavigateSettingsCommand = new RelayCommand(_ => CurrentView = _settingsViewModel);
        }

        public ViewModelBase CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public ICommand NavigateHomeCommand { get; }
        public ICommand NavigateSettingsCommand { get; }
    }
}





