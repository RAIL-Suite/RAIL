using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using RailStudio.Models;
using RailStudio.ViewModels;
using System.Linq;
using System.Collections.Specialized;

namespace RailStudio;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new ViewModels.MainViewModel();
        DataContext = viewModel;
        
        // Subscribe to Packages changes to regenerate columns
        viewModel.Packages.CollectionChanged += Packages_CollectionChanged;
    }
    
    private void Packages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Regenerate DataGrid columns when packages change
        GenerateDynamicParameterColumns();
    }
    
    /// <summary>
    /// Generates dynamic parameter columns based on max parameter count across all tools.
    /// </summary>
    private void GenerateDynamicParameterColumns()
    {
        var viewModel = DataContext as ViewModels.MainViewModel;
        if (viewModel == null || PreviewDataGrid == null)
            return;
        
        // Find max parameter count
        int maxParams = viewModel.Packages
            .SelectMany(p => p.ParsedParameters)
            .GroupBy(p => p)
            .Select(g => g.Count())
            .DefaultIfEmpty(0)
            .Max();
        
        // Actually count by tool
        maxParams = viewModel.Packages
            .Select(tool => tool.ParsedParameters.Count)
            .DefaultIfEmpty(0)
            .Max();
        
        // Clear existing columns except Name, Assembly, Type, Description (4 fixed columns)
        while (PreviewDataGrid.Columns.Count > 4)
        {
            PreviewDataGrid.Columns.RemoveAt(4);
        }
        
        // Add dynamic parameter columns
        for (int i = 0; i < maxParams; i++)
        {
            int paramIndex = i; // Capture for closure
            
            var column = new DataGridTemplateColumn
            {
                Header = $"Param {i + 1}",
                Width = new DataGridLength(150, DataGridLengthUnitType.Pixel)
            };
            
            // Cell template
            var cellTemplate = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(TextBlock));
            
            // Binding to ParsedParameters[i]
            var binding = new MultiBinding();
            binding.Bindings.Add(new Binding($"ParsedParameters[{paramIndex}].Name"));
            binding.Bindings.Add(new Binding($"ParsedParameters[{paramIndex}].TypeDisplay"));
            binding.Converter = new ParameterFormatConverter();
            binding.ConverterParameter = paramIndex;
            
            factory.SetBinding(TextBlock.TextProperty, binding);
            factory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(100, 181, 246))); // #64B5F6
            factory.SetValue(TextBlock.MarginProperty, new Thickness(5, 2, 5, 2));
            
            cellTemplate.VisualTree = factory;
            column.CellTemplate = cellTemplate;
            
            PreviewDataGrid.Columns.Add(column);
        }
    }

    private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem item && item.DataContext is FileSystemNode node)
        {
            var viewModel = DataContext as ViewModels.MainViewModel;
            viewModel?.SelectFileCommand.Execute(node);
            e.Handled = true;
        }
    }
    
    private void AssetTreeViewItem_Selected(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem item && item.DataContext is FileSystemNode node)
        {
            var viewModel = DataContext as ViewModels.MainViewModel;
            
            // If selected file is Rail.manifest.json, load preview
            if (!node.IsDirectory && node.Name == Constants.Rail_MANIFEST_FILENAME)
            {
                viewModel?.LoadPreviewFromAsset(node.Path);
            }
            
            e.Handled = true;
        }
    }


}

/// <summary>
/// Converter for formatting parameter display: "paramName (type)"
/// </summary>
public class ParameterFormatConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (values == null || values.Length < 2)
            return string.Empty;
        
        var name = values[0] as string;
        var type = values[1] as string;
        
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(type))
            return string.Empty;
        
        return $"{name} ({type})";
    }
    
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}



