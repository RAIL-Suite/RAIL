using System.IO;
using Microsoft.Data.Sqlite;
using WorkflowDemo.Models;

namespace WorkflowDemo.Services;

public class DatabaseService
{
    private readonly string _connectionString;
    private static DatabaseService? _instance;
    public static DatabaseService Instance => _instance ??= new DatabaseService();

    private DatabaseService()
    {
        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "demo.db");
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private SqliteConnection GetConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private void InitializeDatabase()
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Products (
                Code TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Description TEXT,
                SalePrice REAL,
                ProductionTimeMinutes INTEGER
            );

            CREATE TABLE IF NOT EXISTS Components (
                Code TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Supplier TEXT,
                UnitCost REAL,
                QtyInStock INTEGER,
                QtyMinimum INTEGER,
                LeadTimeDays INTEGER,
                Location TEXT
            );

            CREATE TABLE IF NOT EXISTS BOM (
                ProductCode TEXT,
                ComponentCode TEXT,
                QtyRequired INTEGER,
                PRIMARY KEY (ProductCode, ComponentCode)
            );

            CREATE TABLE IF NOT EXISTS Machines (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Status TEXT DEFAULT 'free',
                BusyUntil TEXT,
                CurrentOrderId INTEGER
            );

            CREATE TABLE IF NOT EXISTS MachineQueue (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MachineId TEXT,
                OrderId INTEGER,
                Priority INTEGER DEFAULT 0,
                EstimatedMinutes INTEGER,
                ScheduledStart TEXT,
                ScheduledEnd TEXT
            );

            CREATE TABLE IF NOT EXISTS Orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Customer TEXT NOT NULL,
                ProductCode TEXT NOT NULL,
                Quantity INTEGER NOT NULL,
                Status TEXT DEFAULT 'pending',
                CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP,
                RequestedDate TEXT,
                EstimatedCompletion TEXT,
                ActualCompletion TEXT,
                AssignedMachineId TEXT
            );
        ";
        cmd.ExecuteNonQuery();
        
        SeedDataIfEmpty(conn);
    }

    private void SeedDataIfEmpty(SqliteConnection conn)
    {
        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM Products";
        var count = Convert.ToInt32(checkCmd.ExecuteScalar());
        
        if (count > 0) return;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            -- Products
            INSERT INTO Products VALUES ('PUMP-001', 'Standard Hydraulic Pump', 'Industrial pump system', 250.00, 30);
            INSERT INTO Products VALUES ('PUMP-002', 'Heavy Duty Hydraulic Pump', 'High pressure pump', 450.00, 45);
            INSERT INTO Products VALUES ('VALVE-001', 'Flow Control Valve', 'Electronic valve', 180.00, 60);

            -- Components
            INSERT INTO Components VALUES ('FRM-100', 'Aluminum Frame 100mm', 'MetalPro Inc', 15.00, 100, 20, 5, 'A-01-01');
            INSERT INTO Components VALUES ('FRM-150', 'Aluminum Frame 150mm', 'MetalPro Inc', 22.00, 75, 15, 5, 'A-01-02');
            INSERT INTO Components VALUES ('MTR-DC12', 'DC Motor 12V', 'MotorTech GmbH', 45.00, 50, 10, 7, 'B-02-01');
            INSERT INTO Components VALUES ('MTR-DC24', 'DC Motor 24V', 'MotorTech GmbH', 62.00, 35, 8, 7, 'B-02-02');
            INSERT INTO Components VALUES ('PCB-CTRL', 'Main Control Board', 'ElectroParts Inc', 12.00, 200, 30, 3, 'C-01-01');
            INSERT INTO Components VALUES ('PCB-PWR', 'Power Supply Board', 'ElectroParts Inc', 8.00, 180, 25, 3, 'C-01-02');
            INSERT INTO Components VALUES ('SNS-TEMP', 'Temperature Sensor PT100', 'SensorCo Ltd', 8.50, 80, 15, 4, 'C-02-01');
            INSERT INTO Components VALUES ('SNS-PRES', 'Pressure Sensor 0-10bar', 'SensorCo Ltd', 14.00, 60, 12, 4, 'C-02-02');
            INSERT INTO Components VALUES ('HSG-PMP', 'Pump Housing', 'PlastiForm Inc', 5.00, 150, 25, 2, 'D-01-01');
            INSERT INTO Components VALUES ('HSG-VLV', 'Valve Housing', 'PlastiForm Inc', 4.50, 120, 20, 2, 'D-01-02');
            INSERT INTO Components VALUES ('SEAL-25', 'Gasket Ø25mm', 'GasketWorld', 0.80, 500, 100, 1, 'E-01-01');
            INSERT INTO Components VALUES ('SEAL-40', 'Gasket Ø40mm', 'GasketWorld', 1.20, 400, 80, 1, 'E-01-02');

            -- BOM PUMP-001
            INSERT INTO BOM VALUES ('PUMP-001', 'FRM-100', 1);
            INSERT INTO BOM VALUES ('PUMP-001', 'MTR-DC12', 1);
            INSERT INTO BOM VALUES ('PUMP-001', 'PCB-CTRL', 1);
            INSERT INTO BOM VALUES ('PUMP-001', 'PCB-PWR', 1);
            INSERT INTO BOM VALUES ('PUMP-001', 'HSG-PMP', 1);
            INSERT INTO BOM VALUES ('PUMP-001', 'SEAL-25', 2);

            -- BOM PUMP-002
            INSERT INTO BOM VALUES ('PUMP-002', 'FRM-150', 1);
            INSERT INTO BOM VALUES ('PUMP-002', 'MTR-DC24', 1);
            INSERT INTO BOM VALUES ('PUMP-002', 'PCB-CTRL', 1);
            INSERT INTO BOM VALUES ('PUMP-002', 'PCB-PWR', 1);
            INSERT INTO BOM VALUES ('PUMP-002', 'SNS-PRES', 1);
            INSERT INTO BOM VALUES ('PUMP-002', 'HSG-PMP', 1);
            INSERT INTO BOM VALUES ('PUMP-002', 'SEAL-40', 2);

            -- BOM VALVE-001
            INSERT INTO BOM VALUES ('VALVE-001', 'FRM-100', 1);
            INSERT INTO BOM VALUES ('VALVE-001', 'PCB-CTRL', 1);
            INSERT INTO BOM VALUES ('VALVE-001', 'SNS-TEMP', 1);
            INSERT INTO BOM VALUES ('VALVE-001', 'SNS-PRES', 1);
            INSERT INTO BOM VALUES ('VALVE-001', 'HSG-VLV', 1);
            INSERT INTO BOM VALUES ('VALVE-001', 'SEAL-25', 2);

            -- Machines
            INSERT INTO Machines VALUES ('M1', 'Assembly Line 1', 'busy', datetime('now', '+2 hours'), NULL);
            INSERT INTO Machines VALUES ('M2', 'Assembly Line 2', 'free', NULL, NULL);
            INSERT INTO Machines VALUES ('M3', 'Assembly Line 3', 'error', NULL, NULL);

            -- Sample Orders
            INSERT INTO Orders (Customer, ProductCode, Quantity, Status, AssignedMachineId) 
            VALUES ('Acme Corp', 'PUMP-001', 50, 'in_progress', 'M1');
            INSERT INTO Orders (Customer, ProductCode, Quantity, Status) 
            VALUES ('Smith Industries', 'VALVE-001', 20, 'pending');
            INSERT INTO Orders (Customer, ProductCode, Quantity, Status) 
            VALUES ('Johnson & Co', 'PUMP-002', 10, 'pending');
        ";
        cmd.ExecuteNonQuery();
    }

    // ======= PRODUCTS =======
    public List<Product> GetAllProducts()
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Products ORDER BY Code";
        
        var products = new List<Product>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            products.Add(new Product
            {
                Code = reader.GetString(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                SalePrice = reader.GetDecimal(3),
                ProductionTimeMinutes = reader.GetInt32(4)
            });
        }
        return products;
    }

    // ======= COMPONENTS =======
    public List<Component> GetAllComponents()
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Components ORDER BY Code";
        
        var components = new List<Component>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            components.Add(new Component
            {
                Code = reader.GetString(0),
                Name = reader.GetString(1),
                Supplier = reader.IsDBNull(2) ? "" : reader.GetString(2),
                UnitCost = reader.GetDecimal(3),
                QtyInStock = reader.GetInt32(4),
                QtyMinimum = reader.GetInt32(5),
                LeadTimeDays = reader.GetInt32(6),
                Location = reader.IsDBNull(7) ? "" : reader.GetString(7)
            });
        }
        return components;
    }

    public void UpdateComponentStock(string code, int newQty)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Components SET QtyInStock = @qty WHERE Code = @code";
        cmd.Parameters.AddWithValue("@qty", newQty);
        cmd.Parameters.AddWithValue("@code", code);
        cmd.ExecuteNonQuery();
    }

    // ======= BOM =======
    public List<BomItem> GetBom(string productCode)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT b.ProductCode, b.ComponentCode, b.QtyRequired,
                   c.Name, c.UnitCost
            FROM BOM b
            JOIN Components c ON b.ComponentCode = c.Code
            WHERE b.ProductCode = @code
            ORDER BY c.Name";
        cmd.Parameters.AddWithValue("@code", productCode);
        
        var items = new List<BomItem>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new BomItem
            {
                ProductCode = reader.GetString(0),
                ComponentCode = reader.GetString(1),
                QtyRequired = reader.GetInt32(2),
                Component = new Component
                {
                    Code = reader.GetString(1),
                    Name = reader.GetString(3),
                    UnitCost = reader.GetDecimal(4)
                }
            });
        }
        return items;
    }

    // ======= MACHINES =======
    public List<Machine> GetAllMachines()
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Machines ORDER BY Id";
        
        var machines = new List<Machine>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            machines.Add(new Machine
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Status = reader.GetString(2),
                BusyUntil = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                CurrentOrderId = reader.IsDBNull(4) ? null : reader.GetInt32(4)
            });
        }
        return machines;
    }

    public void UpdateMachineStatus(string id, string status, DateTime? busyUntil = null)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Machines SET Status = @status, BusyUntil = @busyUntil WHERE Id = @id";
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@busyUntil", busyUntil?.ToString("s") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    // ======= ORDERS =======
    public List<Order> GetAllOrders()
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM Orders ORDER BY Id DESC";
        
        var orders = new List<Order>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            orders.Add(new Order
            {
                Id = reader.GetInt32(0),
                Customer = reader.GetString(1),
                ProductCode = reader.GetString(2),
                Quantity = reader.GetInt32(3),
                Status = reader.GetString(4),
                CreatedAt = DateTime.Parse(reader.GetString(5)),
                RequestedDate = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
                EstimatedCompletion = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7)),
                ActualCompletion = reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8)),
                AssignedMachineId = reader.IsDBNull(9) ? null : reader.GetString(9)
            });
        }
        return orders;
    }

    public int CreateOrder(string customer, string productCode, int quantity)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Orders (Customer, ProductCode, Quantity, Status, CreatedAt)
            VALUES (@customer, @product, @qty, 'pending', datetime('now'));
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@customer", customer);
        cmd.Parameters.AddWithValue("@product", productCode);
        cmd.Parameters.AddWithValue("@qty", quantity);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void UpdateOrderStatus(int id, string status, string? machineId = null)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Orders SET Status = @status, AssignedMachineId = @machine WHERE Id = @id";
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@machine", machineId ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void ResetDatabase()
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM Orders;
            DELETE FROM MachineQueue;
            DELETE FROM Machines;
            DELETE FROM BOM;
            DELETE FROM Components;
            DELETE FROM Products;
        ";
        cmd.ExecuteNonQuery();
        SeedDataIfEmpty(conn);
    }
}





