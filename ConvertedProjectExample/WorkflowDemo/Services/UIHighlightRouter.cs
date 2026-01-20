using System.Windows;
//using RailFactory.Core.Events;
using WorkflowDemo.ViewModels;

namespace WorkflowDemo.Services;

/// <summary>
/// Routes function call events to UI highlighting actions.
/// Navigates to tabs and highlights relevant elements during LLM function execution.
/// </summary>
public class UIHighlightRouter
{
    private readonly MainViewModel _mainViewModel;
    private readonly int _highlightDelayMs;

    public UIHighlightRouter(MainViewModel mainViewModel, int highlightDelayMs = 300)
    {
        _mainViewModel = mainViewModel;
        _highlightDelayMs = highlightDelayMs;
    }

    /// <summary>
    /// Handle a function call event and route to appropriate UI action.
    /// </summary>
    //public async Task HandleFunctionCallAsync(FunctionCallEvent evt)
    //{
    //    if (evt.Phase != "before") return;

    //    await Application.Current.Dispatcher.InvokeAsync(async () =>
    //    {
    //        // Clear previous highlights first
    //        ClearAllHighlights();

    //        var funcName = evt.FunctionName;
    //        var parameters = evt.Parameters;

    //        switch (funcName)
    //        {
    //            // Products (Tab Index 3)
    //            case "GetAllProducts":
    //                System.Diagnostics.Debug.WriteLine($"[UIRouter] Highlighting ALL products");
    //                NavigateToTab(3);
    //                HighlightAllProducts();
    //                break;

    //            case "GetProduct":
    //            case "GetProductBOM":
    //            case "GetProductCost":
    //                NavigateToTab(3);
    //                if (parameters.TryGetValue("productCode", out var productCode))
    //                {
    //                    System.Diagnostics.Debug.WriteLine($"[UIRouter] Highlighting product: {productCode}");
    //                    HighlightProduct(productCode?.ToString());
    //                }
    //                break;

    //            // Inventory (Tab Index 2)
    //            case "CheckInventory":
    //                NavigateToTab(2);
    //                if (parameters.TryGetValue("componentCode", out var componentCode))
    //                {
    //                    System.Diagnostics.Debug.WriteLine($"[UIRouter] Highlighting component: {componentCode}");
    //                    HighlightComponent(componentCode?.ToString());
    //                }
    //                break;

    //            case "CheckProductAvailability":
    //                NavigateToTab(2);
    //                if (parameters.TryGetValue("productCode", out var pCode))
    //                    HighlightProductComponents(pCode?.ToString());
    //                break;

    //            case "GetAllComponents":
    //                NavigateToTab(2);
    //                HighlightAllComponents();
    //                break;

    //            // Machines (Tab Index 1)
    //            case "GetAllMachinesStatus":
    //            case "GetAllMachines":
    //                System.Diagnostics.Debug.WriteLine($"[UIRouter] Highlighting ALL machines");
    //                NavigateToTab(1);
    //                HighlightAllMachines();
    //                break;

    //            case "GetMachineStatus":
    //            case "SetMachineStatus":
    //                NavigateToTab(1);
    //                if (parameters.TryGetValue("machineId", out var machineId))
    //                    HighlightMachine(machineId?.ToString());
    //                break;

    //            case "GetNextAvailableMachine":
    //                NavigateToTab(1);
    //                HighlightFreeMachines();
    //                break;

    //            // Orders (Tab Index 4)
    //            case "GetAllOrders":
    //            case "GetPendingOrders":
    //                System.Diagnostics.Debug.WriteLine($"[UIRouter] Highlighting ALL orders");
    //                NavigateToTab(4);
    //                HighlightAllOrders();
    //                break;

    //            case "CreateOrder":
    //                NavigateToTab(4);
    //                break;

    //            case "AutoScheduleOrder":
    //                NavigateToTab(4);
    //                if (parameters.TryGetValue("orderId", out var orderId))
    //                    HighlightOrder(orderId?.ToString());
    //                break;

    //            case "UpdateOrderStatus":
    //                NavigateToTab(4);
    //                if (parameters.TryGetValue("id", out var orderIdNum))
    //                    HighlightOrder(orderIdNum?.ToString());
    //                break;
    //        }

    //        // Small delay to let animation show
    //        await Task.Delay(_highlightDelayMs);
    //    });
    //}

    #region Navigation

    private void NavigateToTab(int tabIndex)
    {
        _mainViewModel.SelectedTabIndex = tabIndex;
    }

    #endregion

    #region Highlighting Methods

    private void ClearAllHighlights()
    {
        foreach (var product in _mainViewModel.Products.Products)
            product.IsHighlighted = false;

        foreach (var component in _mainViewModel.Inventory.Components)
            component.IsHighlighted = false;

        foreach (var machine in _mainViewModel.Machines.Machines)
            machine.IsHighlighted = false;

        foreach (var order in _mainViewModel.Orders.Orders)
            order.IsHighlighted = false;
    }

    private void HighlightAllProducts()
    {
        foreach (var product in _mainViewModel.Products.Products)
            product.IsHighlighted = true;
    }

    private void HighlightProduct(string? productCode)
    {
        if (string.IsNullOrEmpty(productCode)) return;
        
        var product = _mainViewModel.Products.Products
            .FirstOrDefault(p => p.Code == productCode);
        if (product != null)
        {
            product.IsHighlighted = true;
            _mainViewModel.Products.SelectedProduct = product;
        }
    }

    private void HighlightAllComponents()
    {
        foreach (var component in _mainViewModel.Inventory.Components)
            component.IsHighlighted = true;
    }

    private void HighlightComponent(string? componentCode)
    {
        if (string.IsNullOrEmpty(componentCode)) return;
        
        var component = _mainViewModel.Inventory.Components
            .FirstOrDefault(c => c.Code == componentCode);
        if (component != null)
        {
            component.IsHighlighted = true;
            _mainViewModel.Inventory.SelectedComponent = component;
        }
    }

    private void HighlightProductComponents(string? productCode)
    {
        if (string.IsNullOrEmpty(productCode)) return;
        
        // Get BOM for this product and highlight all components
        var bom = DatabaseService.Instance.GetBom(productCode);
        foreach (var bomItem in bom)
        {
            var component = _mainViewModel.Inventory.Components
                .FirstOrDefault(c => c.Code == bomItem.ComponentCode);
            if (component != null)
                component.IsHighlighted = true;
        }
    }

    private void HighlightAllMachines()
    {
        foreach (var machine in _mainViewModel.Machines.Machines)
            machine.IsHighlighted = true;
    }

    private void HighlightMachine(string? machineId)
    {
        if (string.IsNullOrEmpty(machineId)) return;
        
        var machine = _mainViewModel.Machines.Machines
            .FirstOrDefault(m => m.Id == machineId);
        if (machine != null)
        {
            machine.IsHighlighted = true;
            _mainViewModel.Machines.SelectedMachine = machine;
        }
    }

    private void HighlightFreeMachines()
    {
        foreach (var machine in _mainViewModel.Machines.Machines.Where(m => m.IsFree))
            machine.IsHighlighted = true;
    }

    private void HighlightAllOrders()
    {
        foreach (var order in _mainViewModel.Orders.Orders)
            order.IsHighlighted = true;
    }

    private void HighlightOrder(string? orderId)
    {
        if (string.IsNullOrEmpty(orderId)) return;
        
        if (int.TryParse(orderId, out var id))
        {
            var order = _mainViewModel.Orders.Orders.FirstOrDefault(o => o.Id == id);
            if (order != null)
            {
                order.IsHighlighted = true;
                _mainViewModel.Orders.SelectedOrder = order;
            }
        }
    }

    #endregion
}





