using AgentTest.Models;
using System.Collections.ObjectModel;
using System.Windows;

namespace AgentTest.Services
{
    /// <summary>
    /// In-memory database for managing customers
    /// </summary>
    public class CustomerDatabase
    {
        private readonly ObservableCollection<Customer> _customers;
        private int _nextId = 1;

        public ObservableCollection<Customer> Customers => _customers;

        public CustomerDatabase()
        {
            _customers = new ObservableCollection<Customer>();
            
            // Add sample data
            AddSampleData();
        }

        private void AddSampleData()
        {
            Add("Mario", "Rossi", "mario.rossi@email.com", "333-1234567", "Via Roma 1, Milano");
            Add("Laura", "Bianchi", "laura.bianchi@email.com", "333-7654321", "Corso Italia 10, Roma");
            Add("Giuseppe", "Verdi", "giuseppe.verdi@email.com", "333-9876543", "Piazza Duomo 5, Firenze");
        }

        public Customer Add(string firstName, string lastName, string email, string phone = "", string address = "")
        {
            var customer = new Customer
            {
                Id = _nextId++,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Phone = phone,
                Address = address,
                CreatedDate = DateTime.Now
            };

            Application.Current.Dispatcher.Invoke(() => _customers.Add(customer));
            return customer;
        }

        public bool Delete(int id)
        {
            var customer = GetById(id);
            if (customer != null)
            {
                Application.Current.Dispatcher.Invoke(() => _customers.Remove(customer));
                return true;
            }
            return false;
        }

        public bool Update(int id, string? firstName = null, string? lastName = null, 
            string? email = null, string? phone = null, string? address = null)
        {
            var customer = GetById(id);
            if (customer == null) return false;

            if (firstName != null) customer.FirstName = firstName;
            if (lastName != null) customer.LastName = lastName;
            if (email != null) customer.Email = email;
            if (phone != null) customer.Phone = phone;
            if (address != null) customer.Address = address;

            return true;
        }

        public Customer? GetById(int id)
        {
            return _customers.FirstOrDefault(c => c.Id == id);
        }

        public List<Customer> Search(string query)
        {
            query = query.ToLower();
            return _customers.Where(c =>
                c.FirstName.ToLower().Contains(query) ||
                c.LastName.ToLower().Contains(query) ||
                c.Email.ToLower().Contains(query)
            ).ToList();
        }

        public List<Customer> GetAll()
        {
            return _customers.ToList();
        }
    }
}







