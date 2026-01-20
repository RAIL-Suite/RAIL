using AgentTest.Models;
using AgentTest.Services;
using AgentTest.Windows;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace AgentTest.ViewModels
{
    /// <summary>
    /// Main ViewModel - All public methods are callable by LLM via EngineLibrary
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly CustomerDatabase _database;
        private DrawingWindow? _drawingWindow;

        public ObservableCollection<Customer> Customers => _database.Customers;

        public MainViewModel()
        {
            _database = new CustomerDatabase();
        }

        #region Customer CRUD - LLM Callable Methods

        /// <summary>
        /// Adds a new customer to the database
        /// </summary>
        public string AddCustomer(string firstName, string lastName, string email, string phone = "", string address = "")
        {
            var customer = _database.Add(firstName, lastName, email, phone, address);
            return $"✓ Cliente aggiunto: {customer.FullName} (ID: {customer.Id})";
        }

        /// <summary>
        /// Deletes a customer by ID
        /// </summary>
        public string DeleteCustomer(int id)
        {
            var customer = _database.GetById(id);
            if (customer == null)
                return $"✗ Cliente con ID {id} non trovato";

            var name = customer.FullName;
            _database.Delete(id);
            return $"✓ Cliente eliminato: {name}";
        }

        /// <summary>
        /// Updates customer information
        /// </summary>
        public string UpdateCustomer(int id, string? firstName = null, string? lastName = null, 
            string? email = null, string? phone = null, string? address = null)
        {
            var customer = _database.GetById(id);
            if (customer == null)
                return $"✗ Cliente con ID {id} non trovato";

            _database.Update(id, firstName, lastName, email, phone, address);
            return $"✓ Cliente aggiornato: {customer.FullName}";
        }

        /// <summary>
        /// Shows customer details in a dialog window
        /// </summary>
        public string ShowCustomerDetails(int id)
        {
            var customer = _database.GetById(id);
            if (customer == null)
                return $"✗ Cliente con ID {id} non trovato";

            // Must run on UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dialog = new CustomerDialog(customer);
                dialog.ShowDialog();
            });

            return $"✓ Mostrati dettagli di: {customer.FullName}";
        }

        /// <summary>
        /// Searches customers by name or email
        /// </summary>
        public string SearchCustomers(string query)
        {
            var results = _database.Search(query);
            if (results.Count == 0)
                return $"✗ Nessun cliente trovato per: {query}";

            var names = string.Join(", ", results.Select(c => $"{c.FullName} (ID: {c.Id})"));
            return $"✓ Trovati {results.Count} clienti: {names}";
        }

        /// <summary>
        /// Gets all customers
        /// </summary>
        public string GetAllCustomers()
        {
            var all = _database.GetAll();
            var names = string.Join("\n", all.Select(c => $"ID {c.Id}: {c.FullName} - {c.Email}"));
            return $"Totale clienti: {all.Count}\n{names}";
        }

        #endregion

        #region Drawing - LLM Callable Methods

        /// <summary>
        /// Opens drawing window and draws using LLM-generated points
        /// LLM generates the coordinates for any shape it wants to draw
        /// </summary>
        public string DrawPoints(List<Dictionary<string, double>> points, string color = "Black", double thickness = 2)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_drawingWindow == null || !_drawingWindow.IsLoaded)
                {
                    _drawingWindow = new DrawingWindow();
                    _drawingWindow.WindowState = WindowState.Maximized;
                    _drawingWindow.Show();
                }
                
                // Force window to foreground
                _drawingWindow.Activate();
                _drawingWindow.Topmost = true;
                _drawingWindow.Topmost = false;
                _drawingWindow.Focus();

                _drawingWindow.executeDP(points, color, thickness);
            });

            return $"✓ Disegnati {points.Count} punti in colore {color}";
        }
        
        /// <summary>
        /// Returns current canvas dimensions and center point.
        /// Use this before drawing to get coordinates for "center" or positioning.
        /// If window is not open, returns screen dimensions (since we open maximized).
        /// </summary>
        public Dictionary<string, double> GetCanvasInfo()
        {
            var info = new Dictionary<string, double>();
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_drawingWindow != null && _drawingWindow.IsLoaded)
                {
                    // Window is open - use actual canvas dimensions
                    var canvas = _drawingWindow.DrawingCanvas;
                    info["width"] = canvas.ActualWidth;
                    info["height"] = canvas.ActualHeight;
                    info["centerX"] = canvas.ActualWidth / 2;
                    info["centerY"] = canvas.ActualHeight / 2;
                }
                else
                {
                    // Window not yet open - use screen dimensions
                    // (we open maximized, so this is accurate)
                    double width = SystemParameters.PrimaryScreenWidth;
                    double height = SystemParameters.PrimaryScreenHeight - 40; // Minus taskbar
                    
                    info["width"] = width;
                    info["height"] = height;
                    info["centerX"] = width / 2;
                    info["centerY"] = height / 2;
                }
            });
            
            return info;
        }

        /// <summary>
        /// Clears all drawings
        /// </summary>
        public string ClearDrawing()
        {
            if (_drawingWindow != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _drawingWindow.Clear();
                });
                return "✓ Disegno cancellato";
            }
            return "✗ Finestra disegno non aperta";
        }

        /// <summary>
        /// Draws a circle - Math done internally, LLM just provides parameters
        /// </summary>
        public string DrawCircle(double centerX, double centerY, double radius, string color = "Black", double thickness = 2)
        {
            var points = new List<Dictionary<string, double>>();
            for (double angle = 0; angle <= 360; angle += 2)
            {
                double radians = angle * Math.PI / 180;
                double x = centerX + radius * Math.Cos(radians);
                double y = centerY + radius * Math.Sin(radians);
                points.Add(new Dictionary<string, double> { { "x", x }, { "y", y } });
            }
            return DrawPoints(points, color, thickness);
        }

        /// <summary>
        /// Draws a rectangle
        /// </summary>
        public string DrawRectangle(double x, double y, double width, double height, string color = "Black", double thickness = 2)
        {
            var points = new List<Dictionary<string, double>>
            {
                new() { { "x", x }, { "y", y } },
                new() { { "x", x + width }, { "y", y } },
                new() { { "x", x + width }, { "y", y + height } },
                new() { { "x", x }, { "y", y + height } },
                new() { { "x", x }, { "y", y } }
            };
            return DrawPoints(points, color, thickness);
        }

        /// <summary>
        /// Draws a line
        /// </summary>
        public string DrawLine(double x1, double y1, double x2, double y2, string color = "Black", double thickness = 2)
        {
            var points = new List<Dictionary<string, double>>
            {
                new() { { "x", x1 }, { "y", y1 } },
                new() { { "x", x2 }, { "y", y2 } }
            };
            return DrawPoints(points, color, thickness);
        }

        /// <summary>
        /// Draws an ellipse
        /// </summary>
        public string DrawEllipse(double centerX, double centerY, double radiusX, double radiusY, string color = "Black", double thickness = 2)
        {
            var points = new List<Dictionary<string, double>>();
            for (double angle = 0; angle <= 360; angle += 2)
            {
                double radians = angle * Math.PI / 180;
                double x = centerX + radiusX * Math.Cos(radians);
                double y = centerY + radiusY * Math.Sin(radians);
                points.Add(new Dictionary<string, double> { { "x", x }, { "y", y } });
            }
            return DrawPoints(points, color, thickness);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}







