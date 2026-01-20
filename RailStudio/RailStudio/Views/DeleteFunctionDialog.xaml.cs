using System.Windows;

namespace RailStudio.Views
{
    public partial class DeleteFunctionDialog : Window
    {
        public bool Confirmed { get; private set; }

        public DeleteFunctionDialog(string functionSummary)
        {
            InitializeComponent();
            FunctionSummaryText.Text = functionSummary;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            DialogResult = true;
            Close();
        }
    }
}




