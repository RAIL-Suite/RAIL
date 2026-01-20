using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WorkflowDemo.Models;
using WorkflowDemo.Services;

namespace WorkflowDemo.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public DashboardViewModel Dashboard { get; } = new();
    public MachinesViewModel Machines { get; } = new();
    public InventoryViewModel Inventory { get; } = new();
    public ProductsViewModel Products { get; } = new();
    public OrdersViewModel Orders { get; } = new();

    public MainViewModel()
    {
        RefreshAll();
    }

    [RelayCommand]
    public void RefreshAll()
    {
        Dashboard.Refresh();
        Machines.Refresh();
        Inventory.Refresh();
        Products.Refresh();
        Orders.Refresh();
        StatusMessage = $"Updated at {DateTime.Now:HH:mm:ss}";
    }

    [RelayCommand]
    public void ResetDemo()
    {
        DatabaseService.Instance.ResetDatabase();
        RefreshAll();
        StatusMessage = "Database reset to demo data";
    }
}

public partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty] private int _machinesFree;
    [ObservableProperty] private int _machinesBusy;
    [ObservableProperty] private int _machinesError;
    [ObservableProperty] private int _ordersPending;
    [ObservableProperty] private int _ordersInProgress;
    [ObservableProperty] private int _ordersCompleted;
    [ObservableProperty] private int _inventoryCritical;
    [ObservableProperty] private int _inventoryLow;
    [ObservableProperty] private int _inventoryOk;

    public ObservableCollection<string> Alerts { get; } = new();

    public void Refresh()
    {
        var machines = DatabaseService.Instance.GetAllMachines();
        MachinesFree = machines.Count(m => m.Status == "free");
        MachinesBusy = machines.Count(m => m.Status == "busy");
        MachinesError = machines.Count(m => m.Status == "error");

        var orders = DatabaseService.Instance.GetAllOrders();
        OrdersPending = orders.Count(o => o.Status == "pending");
        OrdersInProgress = orders.Count(o => o.Status == "in_progress" || o.Status == "scheduled");
        OrdersCompleted = orders.Count(o => o.Status == "completed");

        var components = DatabaseService.Instance.GetAllComponents();
        InventoryCritical = components.Count(c => c.QtyInStock <= 0);
        InventoryLow = components.Count(c => c.QtyInStock > 0 && c.QtyInStock <= c.QtyMinimum);
        InventoryOk = components.Count(c => c.QtyInStock > c.QtyMinimum);

        Alerts.Clear();
        foreach (var m in machines.Where(m => m.Status == "error"))
            Alerts.Add($"🔴 {m.Name} in error state");
        foreach (var c in components.Where(c => c.QtyInStock <= c.QtyMinimum))
            Alerts.Add($"⚠️ {c.Code} below minimum stock ({c.QtyInStock}/{c.QtyMinimum})");
    }
}

public partial class MachinesViewModel : ObservableObject
{
    public ObservableCollection<Machine> Machines { get; } = new();

    [ObservableProperty]
    private Machine? _selectedMachine;

    public void Refresh()
    {
        Machines.Clear();
        foreach (var m in DatabaseService.Instance.GetAllMachines())
            Machines.Add(m);
    }

    [RelayCommand]
    public void SetFree()
    {
        if (SelectedMachine == null) return;
        DatabaseService.Instance.UpdateMachineStatus(SelectedMachine.Id, "free");
        Refresh();
    }

    [RelayCommand]
    public void SetBusy()
    {
        if (SelectedMachine == null) return;
        DatabaseService.Instance.UpdateMachineStatus(SelectedMachine.Id, "busy", DateTime.Now.AddHours(2));
        Refresh();
    }

    [RelayCommand]
    public void SetError()
    {
        if (SelectedMachine == null) return;
        DatabaseService.Instance.UpdateMachineStatus(SelectedMachine.Id, "error");
        Refresh();
    }
}

public partial class InventoryViewModel : ObservableObject
{
    public ObservableCollection<Component> Components { get; } = new();

    [ObservableProperty]
    private Component? _selectedComponent;

    [ObservableProperty]
    private string _filterText = "";

    [ObservableProperty]
    private bool _showOnlyCritical;

    public void Refresh()
    {
        Components.Clear();
        var all = DatabaseService.Instance.GetAllComponents();
        
        foreach (var c in all)
        {
            if (ShowOnlyCritical && c.QtyInStock > c.QtyMinimum) continue;
            if (!string.IsNullOrEmpty(FilterText) && 
                !c.Code.Contains(FilterText, StringComparison.OrdinalIgnoreCase) &&
                !c.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
                continue;
            Components.Add(c);
        }
    }

    partial void OnFilterTextChanged(string value) => Refresh();
    partial void OnShowOnlyCriticalChanged(bool value) => Refresh();

    [RelayCommand]
    public void AddStock()
    {
        if (SelectedComponent == null) return;
        DatabaseService.Instance.UpdateComponentStock(SelectedComponent.Code, SelectedComponent.QtyInStock + 50);
        Refresh();
    }

    [RelayCommand]
    public void RemoveStock()
    {
        if (SelectedComponent == null) return;
        var newQty = Math.Max(0, SelectedComponent.QtyInStock - 10);
        DatabaseService.Instance.UpdateComponentStock(SelectedComponent.Code, newQty);
        Refresh();
    }
}

public partial class ProductsViewModel : ObservableObject
{
    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<BomItem> BomItems { get; } = new();

    [ObservableProperty]
    private Product? _selectedProduct;

    [ObservableProperty]
    private decimal _totalBomCost;

    public void Refresh()
    {
        Products.Clear();
        foreach (var p in DatabaseService.Instance.GetAllProducts())
            Products.Add(p);
        
        if (SelectedProduct != null)
            LoadBom(SelectedProduct.Code);
    }

    partial void OnSelectedProductChanged(Product? value)
    {
        if (value != null)
            LoadBom(value.Code);
    }

    private void LoadBom(string productCode)
    {
        BomItems.Clear();
        var items = DatabaseService.Instance.GetBom(productCode);
        decimal total = 0;
        foreach (var item in items)
        {
            BomItems.Add(item);
            if (item.Component != null)
                total += item.Component.UnitCost * item.QtyRequired;
        }
        TotalBomCost = total;
    }
}

public partial class OrdersViewModel : ObservableObject
{
    public ObservableCollection<Order> Orders { get; } = new();
    public ObservableCollection<Machine> AvailableMachines { get; } = new();

    [ObservableProperty]
    private Order? _selectedOrder;

    [ObservableProperty]
    private string _newCustomer = "";

    [ObservableProperty]
    private string _newProductCode = "";

    [ObservableProperty]
    private int _newQuantity = 1;

    public void Refresh()
    {
        Orders.Clear();
        foreach (var o in DatabaseService.Instance.GetAllOrders())
            Orders.Add(o);

        AvailableMachines.Clear();
        foreach (var m in DatabaseService.Instance.GetAllMachines().Where(m => m.Status == "free"))
            AvailableMachines.Add(m);
    }

    [RelayCommand]
    public void CreateOrder()
    {
        if (string.IsNullOrEmpty(NewCustomer) || string.IsNullOrEmpty(NewProductCode) || NewQuantity <= 0)
            return;

        DatabaseService.Instance.CreateOrder(NewCustomer, NewProductCode, NewQuantity);
        NewCustomer = "";
        NewProductCode = "";
        NewQuantity = 1;
        Refresh();
    }

    [RelayCommand]
    public void ScheduleAuto()
    {
        if (SelectedOrder == null) return;
        
        var freeMachine = DatabaseService.Instance.GetAllMachines().FirstOrDefault(m => m.Status == "free");
        if (freeMachine != null)
        {
            DatabaseService.Instance.UpdateOrderStatus(SelectedOrder.Id, "scheduled", freeMachine.Id);
            DatabaseService.Instance.UpdateMachineStatus(freeMachine.Id, "busy", DateTime.Now.AddHours(2));
        }
        Refresh();
    }

    [RelayCommand]
    public void CompleteOrder()
    {
        if (SelectedOrder == null) return;
        DatabaseService.Instance.UpdateOrderStatus(SelectedOrder.Id, "completed");
        if (SelectedOrder.AssignedMachineId != null)
            DatabaseService.Instance.UpdateMachineStatus(SelectedOrder.AssignedMachineId, "free");
        Refresh();
    }

    [RelayCommand]
    public void CancelOrder()
    {
        if (SelectedOrder == null) return;
        DatabaseService.Instance.UpdateOrderStatus(SelectedOrder.Id, "cancelled");
        if (SelectedOrder.AssignedMachineId != null)
            DatabaseService.Instance.UpdateMachineStatus(SelectedOrder.AssignedMachineId, "free");
        Refresh();
    }
}





