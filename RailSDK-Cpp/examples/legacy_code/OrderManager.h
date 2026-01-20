#pragma once
#include <string>
#include <vector>
#include <iostream>

// ═══════════════════════════════════════════════════════════════
// LEGACY CODE - UNTOUCHED BY Rail
// ═══════════════════════════════════════════════════════════════

struct Order {
    int id;
    std::string client;
    int quantity;
};

class OrderManager {
public:
    OrderManager() = default;

    // Creates a new order and returns true on success
    bool CreateOrder(const std::string& clientName, int quantity) {
        Order o;
        o.id = _nextId++;
        o.client = clientName;
        o.quantity = quantity;
        _orders.push_back(o);
        
        std::cout << "[OrderManager] Created Object #" << o.id 
                  << " for " << clientName 
                  << " (Qty: " << quantity << ")" << std::endl;
        return true;
    }

    // Returns the number of active orders
    int GetOrderCount() const {
        return (int)_orders.size();
    }

    // Cancel an order by ID
    void CancelOrder(int orderId) {
        std::cout << "[OrderManager] Cancelling Order #" << orderId << std::endl;
        // logic to remove...
    }

private:
    std::vector<Order> _orders;
    int _nextId = 1000;
};


