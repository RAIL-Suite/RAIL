using AgentTest.Models;
using System.Windows;

namespace AgentTest.Windows
{
    public partial class CustomerDialog : Window
    {
        public CustomerDialog(Customer customer)
        {
            InitializeComponent();
            DataContext = customer;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}







