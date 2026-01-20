using System.Text.Json;
using WorkflowDemo.Models;
using WorkflowDemo.Services;

namespace WorkflowDemo.RailBridge;

/// <summary>
/// Functions exposed to LLM via RailProtocol.
/// These methods are discovered by RailStudio and callable by RailLLM.
/// </summary>
public class RailFunctions
{
    private readonly DatabaseService _db = DatabaseService.Instance;

    // ============ PRODUCTS ============

    /// <summary>
    /// Get all available products.
    /// </summary>
    /// <returns>List of products with code, name, and production time.</returns>
    public string GetAllProducts()
    {
        var products = _db.GetAllProducts();
        return JsonSerializer.Serialize(products, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Get product details by code.
    /// </summary>
    /// <param name="productCode">The product code (e.g., PUMP-001)</param>
    /// <returns>Product details including name, price, and production time.</returns>
    public string GetProduct(string productCode)
    {
        var product = _db.GetAllProducts().FirstOrDefault(p => p.Code == productCode);
        if (product == null)
            return $"Product {productCode} not found.";
        return JsonSerializer.Serialize(product, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Get the Bill of Materials (BOM) for a product.
    /// </summary>
    /// <param name="productCode">The product code (e.g., PUMP-001)</param>
    /// <returns>List of components required with quantities and costs.</returns>
    public string GetProductBOM(string productCode)
    {
        var bom = _db.GetBom(productCode);
        if (bom.Count == 0)
            return $"No BOM found for product {productCode}.";
        
        var result = bom.Select(b => new {
            Component = b.ComponentCode,
            Name = b.Component?.Name,
            Quantity = b.QtyRequired,
            UnitCost = b.Component?.UnitCost,
            TotalCost = b.Component?.UnitCost * b.QtyRequired
        });
        
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Calculate the total material cost for producing a quantity of a product.
    /// </summary>
    /// <param name="productCode">The product code</param>
    /// <param name="quantity">Number of units to produce</param>
    /// <returns>Cost breakdown with total.</returns>
    public string GetProductCost(string productCode, int quantity)
    {
        var bom = _db.GetBom(productCode);
        if (bom.Count == 0)
            return $"No BOM found for product {productCode}.";

        decimal totalCost = 0;
        var breakdown = new List<object>();
        
        foreach (var item in bom)
        {
            var itemCost = (item.Component?.UnitCost ?? 0) * item.QtyRequired * quantity;
            totalCost += itemCost;
            breakdown.Add(new {
                Component = item.ComponentCode,
                QuantityPerUnit = item.QtyRequired,
                TotalQuantity = item.QtyRequired * quantity,
                Cost = itemCost
            });
        }

        return JsonSerializer.Serialize(new {
            Product = productCode,
            Quantity = quantity,
            Breakdown = breakdown,
            TotalMaterialCost = totalCost
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    // ============ INVENTORY ============

    /// <summary>
    /// Check the current inventory level for a component.
    /// </summary>
    /// <param name="componentCode">The component code (e.g., MTR-DC12)</param>
    /// <returns>Stock level and status.</returns>
    public string CheckInventory(string componentCode)
    {
        var component = _db.GetAllComponents().FirstOrDefault(c => c.Code == componentCode);
        if (component == null)
            return $"Component {componentCode} not found.";

        return JsonSerializer.Serialize(new {
            component.Code,
            component.Name,
            InStock = component.QtyInStock,
            Minimum = component.QtyMinimum,
            Status = component.StockStatus,
            component.Supplier,
            component.Location
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Check if all materials are available to produce a product.
    /// </summary>
    /// <param name="productCode">The product code</param>
    /// <param name="quantity">Number of units to produce</param>
    /// <returns>Availability status with details on any shortages.</returns>
    public string CheckProductAvailability(string productCode, int quantity)
    {
        var bom = _db.GetBom(productCode);
        var components = _db.GetAllComponents();
        
        var shortages = new List<object>();
        bool canProduce = true;

        foreach (var item in bom)
        {
            var comp = components.FirstOrDefault(c => c.Code == item.ComponentCode);
            var needed = item.QtyRequired * quantity;
            var available = comp?.QtyInStock ?? 0;
            
            if (available < needed)
            {
                canProduce = false;
                shortages.Add(new {
                    Component = item.ComponentCode,
                    Needed = needed,
                    Available = available,
                    Shortage = needed - available
                });
            }
        }

        return JsonSerializer.Serialize(new {
            Product = productCode,
            Quantity = quantity,
            CanProduce = canProduce,
            Shortages = shortages
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    // ============ MACHINES ============

    /// <summary>
    /// Get the status of all production machines.
    /// </summary>
    /// <returns>List of machines with their current status.</returns>
    public string GetAllMachinesStatus()
    {
        var machines = _db.GetAllMachines();
        return JsonSerializer.Serialize(machines.Select(m => new {
            m.Id,
            m.Name,
            m.Status,
            BusyUntil = m.BusyUntil?.ToString("HH:mm"),
            CurrentOrder = m.CurrentOrderId
        }), new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Get details of a specific machine.
    /// </summary>
    /// <param name="machineId">The machine ID (e.g., M1)</param>
    public string GetMachineStatus(string machineId)
    {
        var machine = _db.GetAllMachines().FirstOrDefault(m => m.Id == machineId);
        if (machine == null)
            return $"Machine {machineId} not found.";

        return JsonSerializer.Serialize(new {
            machine.Id,
            machine.Name,
            machine.Status,
            BusyUntil = machine.BusyUntil?.ToString("yyyy-MM-dd HH:mm"),
            CurrentOrder = machine.CurrentOrderId
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Find the next machine that will be available and when.
    /// </summary>
    /// <returns>The next available machine with estimated availability time.</returns>
    public string GetNextAvailableMachine()
    {
        var machines = _db.GetAllMachines()
            .Where(m => m.Status != "error")
            .OrderBy(m => m.Status == "free" ? DateTime.MinValue : m.BusyUntil ?? DateTime.MaxValue)
            .FirstOrDefault();

        if (machines == null)
            return "No machines available (all in error state).";

        return JsonSerializer.Serialize(new {
            machines.Id,
            machines.Name,
            machines.Status,
            AvailableAt = machines.Status == "free" ? "NOW" : machines.BusyUntil?.ToString("HH:mm")
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Get all orders.
    /// </summary>
    /// <returns>List of all orders with their status.</returns>
    public string GetAllOrders()
    {
        var orders = _db.GetAllOrders();
        return JsonSerializer.Serialize(orders.Select(o => new {
            o.Id,
            o.Customer,
            Product = o.ProductCode,
            o.Quantity,
            o.Status,
            Machine = o.AssignedMachineId,
            Created = o.CreatedAt.ToString("dd/MM HH:mm")
        }), new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Get orders filtered by status.
    /// </summary>
    /// <param name="status">Status to filter: pending, scheduled, in_progress, completed, cancelled</param>
    /// <returns>List of orders with the specified status.</returns>
    public string GetOrdersByStatus(string status)
    {
        var orders = _db.GetAllOrders().Where(o => o.Status == status);
        return JsonSerializer.Serialize(orders.Select(o => new {
            o.Id,
            o.Customer,
            Product = o.ProductCode,
            o.Quantity,
            o.Status,
            Machine = o.AssignedMachineId,
            Created = o.CreatedAt.ToString("dd/MM HH:mm")
        }), new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Get all pending and in-progress orders (not yet completed).
    /// </summary>
    /// <returns>List of active orders.</returns>
    public string GetActiveOrders()
    {
        var orders = _db.GetAllOrders()
            .Where(o => o.Status == "pending" || o.Status == "scheduled" || o.Status == "in_progress");
        return JsonSerializer.Serialize(orders.Select(o => new {
            o.Id,
            o.Customer,
            Product = o.ProductCode,
            o.Quantity,
            o.Status,
            Machine = o.AssignedMachineId,
            Created = o.CreatedAt.ToString("dd/MM HH:mm")
        }), new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Get orders in progress (currently being produced).
    /// </summary>
    /// <returns>List of orders being produced right now.</returns>
    public string GetOrdersInProgress()
    {
        var orders = _db.GetAllOrders().Where(o => o.Status == "in_progress");
        return JsonSerializer.Serialize(orders.Select(o => new {
            o.Id,
            o.Customer,
            Product = o.ProductCode,
            o.Quantity,
            o.Status,
            Machine = o.AssignedMachineId,
            Created = o.CreatedAt.ToString("dd/MM HH:mm")
        }), new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Get pending orders (waiting to be scheduled).
    /// </summary>
    public string GetPendingOrders()
    {
        var orders = _db.GetAllOrders().Where(o => o.Status == "pending");
        return JsonSerializer.Serialize(orders.Select(o => new {
            o.Id,
            o.Customer,
            Product = o.ProductCode,
            o.Quantity,
            o.Status,
            Created = o.CreatedAt.ToString("dd/MM HH:mm")
        }), new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Estimate when an order would be completed if created now.
    /// </summary>
    /// <param name="productCode">The product code</param>
    /// <param name="quantity">Number of units</param>
    /// <returns>Estimated start and end times for production.</returns>
    public string EstimateOrderCompletion(string productCode, int quantity)
    {
        var product = _db.GetAllProducts().FirstOrDefault(p => p.Code == productCode);
        if (product == null)
            return $"Product {productCode} not found.";

        var productionMinutes = product.ProductionTimeMinutes * quantity;
        
        var nextMachine = _db.GetAllMachines()
            .Where(m => m.Status != "error")
            .OrderBy(m => m.Status == "free" ? DateTime.MinValue : m.BusyUntil ?? DateTime.MaxValue)
            .FirstOrDefault();

        if (nextMachine == null)
            return "No machines available.";

        var startTime = nextMachine.Status == "free" ? DateTime.Now : nextMachine.BusyUntil ?? DateTime.Now;
        var endTime = startTime.AddMinutes(productionMinutes);

        return JsonSerializer.Serialize(new {
            Product = productCode,
            Quantity = quantity,
            Machine = nextMachine.Name,
            ProductionTime = $"{productionMinutes} minutes",
            EstimatedStart = startTime.ToString("dd/MM/yyyy HH:mm"),
            EstimatedEnd = endTime.ToString("dd/MM/yyyy HH:mm")
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Create a new production order.
    /// </summary>
    /// <param name="customer">Customer name</param>
    /// <param name="productCode">Product code to produce</param>
    /// <param name="quantity">Number of units</param>
    /// <returns>Confirmation with order ID.</returns>
    public string CreateOrder(string customer, string productCode, int quantity)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[RailFunctions] CreateOrder called: customer={customer}, product={productCode}, qty={quantity}");
            var orderId = _db.CreateOrder(customer, productCode, quantity);
            System.Diagnostics.Debug.WriteLine($"[RailFunctions] Order created with ID: {orderId}");
            return $"Order #{orderId} created successfully for {customer}: {quantity}x {productCode}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RailFunctions] CreateOrder ERROR: {ex.Message}");
            return $"Error creating order: {ex.Message}";
        }
    }

    /// <summary>
    /// Automatically schedule an order on the next available machine.
    /// </summary>
    /// <param name="orderId">The order ID to schedule</param>
    public string AutoScheduleOrder(int orderId)
    {
        var orders = _db.GetAllOrders();
        var order = orders.FirstOrDefault(o => o.Id == orderId);
        if (order == null)
            return $"Order {orderId} not found.";

        var freeMachine = _db.GetAllMachines().FirstOrDefault(m => m.Status == "free");
        if (freeMachine == null)
        {
            var nextMachine = _db.GetAllMachines()
                .Where(m => m.Status == "busy")
                .OrderBy(m => m.BusyUntil)
                .FirstOrDefault();
            
            if (nextMachine != null)
                return $"No machine currently free. {nextMachine.Name} will be available at {nextMachine.BusyUntil:HH:mm}.";
            
            return "No machines available.";
        }

        var product = _db.GetAllProducts().FirstOrDefault(p => p.Code == order.ProductCode);
        var productionMinutes = (product?.ProductionTimeMinutes ?? 30) * order.Quantity;
        var endTime = DateTime.Now.AddMinutes(productionMinutes);

        _db.UpdateOrderStatus(orderId, "scheduled", freeMachine.Id);
        _db.UpdateMachineStatus(freeMachine.Id, "busy", endTime);

        return $"Order #{orderId} scheduled on {freeMachine.Name}. Estimated completion: {endTime:HH:mm}";
    }

    /// <summary>
    /// Set a machine status (for simulation).
    /// </summary>
    /// <param name="machineId">The machine ID</param>
    /// <param name="status">New status: free, busy, or error</param>
    public string SetMachineStatus(string machineId, string status)
    {
        if (status != "free" && status != "busy" && status != "error")
            return "Invalid status. Use: free, busy, or error.";

        _db.UpdateMachineStatus(machineId, status, status == "busy" ? DateTime.Now.AddHours(2) : null);
        return $"Machine {machineId} status set to {status}.";
    }
}





