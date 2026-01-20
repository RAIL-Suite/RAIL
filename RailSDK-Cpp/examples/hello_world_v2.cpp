/**
 * Example: C++ Application with Rail SDK v2.0
 * 
 * This demonstrates the new fluent API for function registration
 * and automatic manifest generation.
 */

#include <rail/Rail.h>
#include <iostream>
#include <cmath>

// Example function implementations
std::string DoCalculate(const std::string& command_json) {
    // In real code, parse command_json to extract params
    // For demo, return a simple result
    return "{\"status\":\"ok\",\"result\":42}";
}

std::string DoSaveFile(const std::string& command_json) {
    return "{\"status\":\"ok\",\"message\":\"File saved\"}";
}

std::string DoGetStatus(const std::string& command_json) {
    return "{\"status\":\"ok\",\"running\":true,\"temperature\":65.5}";
}

int main() {
    // 1. Create app with name and version
    rail::RailApp app("MyCppApp", "1.0.0");
    
    // 2. Set app description
    app.Description("A sample C++ application controlled by AI agents");
    
    // 3. Register functions with fluent API
    app.RegisterFunction("Calculate", DoCalculate)
       .Description("Performs mathematical calculations")
       .Param("a", "INTEGER", "First operand")
       .Param("b", "INTEGER", "Second operand")
       .Param("operation", "STRING", "Operation: add, subtract, multiply, divide")
       .Returns("INTEGER", "Result of the calculation");
    
    app.RegisterFunction("SaveFile", DoSaveFile)
       .Description("Saves content to a file on disk")
       .Param("path", "STRING", "Absolute file path")
       .Param("content", "STRING", "Content to write to file")
       .Param("overwrite", "BOOLEAN", "If true, overwrites existing file", false) // optional
       .Returns("BOOLEAN", "True if successful");
    
    app.RegisterFunction("GetStatus", DoGetStatus)
       .Description("Gets the current application status")
       .Returns("OBJECT", "Status object with running state and metrics");
    
    // 4. Ignite! This will:
    //    - Check if manifest exists with same version
    //    - Generate manifest if needed (to C:\Work\Project\Alchemy\Json\exetest\MyCppApp\)
    //    - Connect to Rail Host via named pipe
    if (!app.Ignite()) {
        std::cerr << "Failed to ignite app!" << std::endl;
        return 1;
    }
    
    std::cout << "Application running. Press Enter to quit..." << std::endl;
    std::cin.get();
    
    app.Disconnect();
    return 0;
}


