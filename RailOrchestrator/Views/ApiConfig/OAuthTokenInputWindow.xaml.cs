using System.Windows;

namespace WpfRagApp.Views.ApiConfig;

/// <summary>
/// Simple dialog for manual OAuth token input.
/// Used as fallback when automatic callback handling isn't available.
/// </summary>
public partial class OAuthTokenInputWindow : Window
{
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    
    public OAuthTokenInputWindow()
    {
        InitializeComponent();
    }
    
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        AccessToken = AccessTokenInput.Text.Trim();
        RefreshToken = RefreshTokenInput.Text.Trim();
        
        if (string.IsNullOrEmpty(AccessToken))
        {
            MessageBox.Show("Please enter an access token.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        DialogResult = true;
        Close();
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}





