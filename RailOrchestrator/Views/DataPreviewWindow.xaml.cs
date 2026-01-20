using System.Windows;
using WpfRagApp.Services.DataIngestion;
using WpfRagApp.Services.DataIngestion.Models;

namespace WpfRagApp.Views;

/// <summary>
/// Preview window for data import confirmation.
/// </summary>
public partial class DataPreviewWindow : Window
{
    private readonly PreviewData _previewData;
    private MappingResult _confirmedMapping;
    
    /// <summary>
    /// Gets the confirmed mapping after user approval.
    /// </summary>
    public MappingResult? ConfirmedMapping { get; private set; }
    
    /// <summary>
    /// Gets the execution configuration.
    /// </summary>
    public ExecutionConfig? Config { get; private set; }
    
    /// <summary>
    /// True if user clicked Apply.
    /// </summary>
    public bool IsConfirmed { get; private set; }
    
    public DataPreviewWindow(PreviewData previewData)
    {
        InitializeComponent();
        _previewData = previewData;
        _confirmedMapping = previewData.Mapping;
        
        LoadPreviewData();
    }
    
    private void LoadPreviewData()
    {
        // File info
        var fileName = System.IO.Path.GetFileName(_previewData.SourceFile);
        FileInfoText.Text = $"{fileName} • {_previewData.TotalRows:N0} rows";
        
        // Target method
        TargetMethodText.Text = $"{_previewData.TargetMethod.ModuleName}.{_previewData.TargetMethod.MethodName}";
        
        // Apply button text
        ApplyButton.Content = $"Apply ({_previewData.TotalRows:N0} rows)";
        
        // Mapping grid
        var mappingItems = _previewData.Mapping.Mappings.Select(m => new MappingDisplayItem
        {
            SourceColumn = m.SourceColumn,
            TargetParameter = m.TargetParameter,
            Confidence = m.Confidence,
            ConfidencePercent = $"{m.Confidence * 100:F0}%"
        }).ToList();
        
        MappingGrid.ItemsSource = mappingItems;
        
        // Warnings
        if (_previewData.Mapping.Warnings.Any())
        {
            WarningsPanel.Visibility = Visibility.Visible;
            WarningsList.ItemsSource = _previewData.Mapping.Warnings;
        }
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        DialogResult = false;
        Close();
    }
    
    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        IsConfirmed = true;
        ConfirmedMapping = _confirmedMapping;
        
        // Parse rate limit
        if (int.TryParse(RateLimitBox.Text, out var rateLimit) && rateLimit > 0)
        {
            Config = new ExecutionConfig
            {
                MaxCallsPerSecond = rateLimit,
                DelayBetweenCallsMs = 1000 / rateLimit
            };
        }
        else
        {
            Config = ExecutionConfig.Default;
        }
        
        DialogResult = true;
        Close();
    }
}

/// <summary>
/// Display item for mapping grid.
/// </summary>
public class MappingDisplayItem
{
    public string SourceColumn { get; set; } = string.Empty;
    public string TargetParameter { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string ConfidencePercent { get; set; } = string.Empty;
}





