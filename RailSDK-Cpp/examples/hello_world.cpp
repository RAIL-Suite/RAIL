#include <rail/Rail.h>
#include <iostream>
#include <string>
#include <thread>
#include <chrono>

// Define a method exposed to LLM
// Args format: {"param1": "value"}
std::string get_uptime(const std::string& json_args) {
    std::cout << "[App] Executing GetUptime..." << std::endl;
    return "{\"status\":\"success\", \"result\": \"Uptime: 42 seconds\"}";
}

int main() {
    try {
        std::cout << "Starting Rail C++ App..." << std::endl;
        
        // Register method
        rail::register_method("System", "GetUptime", "Returns system uptime", get_uptime);
        
        // Ignite with fixed ID to match static manifest
        std::string appId = "CppApp";

        if (rail::ignite({}, appId)) { 
            std::cout << "Connected to Rail Host! (ID: " << appId << ")" << std::endl;
            std::cout << "Waiting for commands... (Ctrl+C to stop)" << std::endl;
            
            while (rail::is_connected()) {
                std::this_thread::sleep_for(std::chrono::seconds(1));
            }
            
            std::cout << "Disconnected from Host." << std::endl;
        } else {
            std::cerr << "Failed to connect to Rail Host. Is RailLLM running?" << std::endl;
        }
    }
    catch (const std::exception& ex) {
        std::cerr << "CRITICAL ERROR: " << ex.what() << std::endl;
    }
    catch (...) {
        std::cerr << "CRITICAL ERROR: Unknown exception" << std::endl;
    }

    std::cout << "Press ENTER to exit..." << std::endl;
    std::cin.get();
    return 0;
}


