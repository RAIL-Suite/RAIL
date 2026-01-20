namespace AgentTest.Models
{
    /// <summary>
    /// Represents a customer in the database
    /// </summary>
    public class Customer
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }

        public string FullName => $"{FirstName} {LastName}";

        public Customer()
        {
            CreatedDate = DateTime.Now;
        }
    }
}







