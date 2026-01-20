#include <rail/Rail.h>
#include <iostream>
#include <thread>
#include <chrono>
#include <functional>
#include "legacy_code/OrderManager.h"

// Forward declaration of ForceLink function from RailBinding.cpp
void ForceLink_OrderManager();

int main() {
    std::cout << "Starting Rail C++ RTTR Demo..." << std::endl;

    // 1. Force Linker to include bindings
    ForceLink_OrderManager(); 

    // 2. Instantiate Legacy Object (Business Logic)
    OrderManager myOrderManager;
    
    // 3. Register Instance with Rail (The Bridge)
    // Use std::ref to register as a reference, not a copy or pointer.
    // This ensures RTTR invocation sees "OrderManager" type directly.
    rail::RegisterInstance("OrderManager", std::ref(myOrderManager));
    
    // 4. Ignite Rail (Connect to Host)
    // Checks RTTR registry, generates manifest, sends to Host via generic-bridge.dll
    rail::Ignite("CppOrderSystem", "3.0.0");
    
    std::cout << "Application Running. Waiting for AI commands..." << std::endl;

    std::cout << "\n[Test] Verifying JSON Dispatch..." << std::endl;
    std::string testJson = R"({
        "context": "OrderManager",
        "method": "CreateOrder",
        "args": ["TEST-ORDER-1", 5]
    })";
    std::string result = rail::DebugDispatch(testJson);
    std::cout << "[Test] Dispatch Result: " << result << std::endl;
    
    // Verify count
    std::string testCount = R"({
        "context": "OrderManager",
        "method": "GetOrderCount",
        "args": []
    })";
    std::string countResult = rail::DebugDispatch(testCount);
    std::cout << "[Test] Count Result: " << countResult << std::endl;
    // --------------------------------------------

    // 5. Main Loop (Simulating a GUI or Game Loop)
    while (true) {
        // Poll for AI commands on the main thread
        // This ensures thread safety for the legacy code
        rail::ProcessEvents(); 
        
        // Simulating 60 FPS frame
        std::this_thread::sleep_for(std::chrono::milliseconds(16));
    }

    return 0;
}


