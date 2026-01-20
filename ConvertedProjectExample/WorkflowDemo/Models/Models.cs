using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WorkflowDemo.Models;

/// <summary>
/// Base class that provides INotifyPropertyChanged for UI binding.
/// </summary>
public abstract class ObservableModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    private bool _isHighlighted;
    /// <summary>UI highlight state for demo visualization.</summary>
    public bool IsHighlighted
    {
        get => _isHighlighted;
        set { _isHighlighted = value; OnPropertyChanged(); }
    }
}

public class Product : ObservableModel
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal SalePrice { get; set; }
    public int ProductionTimeMinutes { get; set; }
}

public class Component : ObservableModel
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public int QtyInStock { get; set; }
    public int QtyMinimum { get; set; }
    public int LeadTimeDays { get; set; }
    public string Location { get; set; } = string.Empty;
    
    public string StockStatus => QtyInStock <= 0 ? "CRITICO" 
        : QtyInStock <= QtyMinimum ? "BASSO" 
        : "OK";
}

public class BomItem
{
    public string ProductCode { get; set; } = string.Empty;
    public string ComponentCode { get; set; } = string.Empty;
    public int QtyRequired { get; set; }
    
    // Navigation
    public Component? Component { get; set; }
}

public class Machine : ObservableModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "free"; // free, busy, error
    public DateTime? BusyUntil { get; set; }
    public int? CurrentOrderId { get; set; }
    
    public bool IsFree => Status == "free";
    public bool IsBusy => Status == "busy";
    public bool IsError => Status == "error";
}

public class MachineQueueItem
{
    public int Id { get; set; }
    public string MachineId { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public int Priority { get; set; }
    public int EstimatedMinutes { get; set; }
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
}

public class Order : ObservableModel
{
    public int Id { get; set; }
    public string Customer { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Status { get; set; } = "pending"; // pending, scheduled, in_progress, completed, cancelled
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? RequestedDate { get; set; }
    public DateTime? EstimatedCompletion { get; set; }
    public DateTime? ActualCompletion { get; set; }
    public string? AssignedMachineId { get; set; }
    
    // Navigation
    public Product? Product { get; set; }
}

public class Supplier
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
}





