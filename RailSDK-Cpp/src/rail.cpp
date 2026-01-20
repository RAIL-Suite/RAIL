#include "rail/rail.h"
#include "instance_registry.h"
#include <iostream>
#include <fstream>
#include <windows.h>
#include <string>

// Forward declaration from dispatcher.cpp
namespace rail {
    std::string GenerateManifest(const std::string& appName);
    std::string DispatchCommand(const std::string& jsonCmd);
}

// -----------------------------------------------------------------------
// NATIVE BRIDGE TYPES
// -----------------------------------------------------------------------
typedef const char* (*NativeDispatchCallback)(const char* commandJson);
typedef int (*LiqIgniteFunc)(const char* instanceId, const char* jsonManifest, NativeDispatchCallback callback);
typedef void (*LiqDisconnectFunc)();

// Global handle to the DLL
HMODULE g_hBridgeDll = nullptr;
LiqIgniteFunc g_fnIgnite = nullptr;
LiqDisconnectFunc g_fnDisconnect = nullptr;

namespace rail {

    // Global state
    static bool g_Connected = false;
    static std::string g_LastResult; // Buffer for returning string to unmanaged code (thread unsafe if single global, but sufficient for proof of concept)
    static std::function<std::string(const std::string&)> g_CustomDispatcher = nullptr;

    void SetCustomDispatcher(std::function<std::string(const std::string&)> dispatcher) {
        g_CustomDispatcher = dispatcher;
    }

    // -----------------------------------------------------------------------
    // CALLBACK (Called by RailBridge.dll on a background thread)
    // -----------------------------------------------------------------------
    const char* BridgeCallback(const char* commandJson) {
        if (!commandJson) return "{\"error\":\"null_command\"}";
        
        // 1. Invoke the Dispatcher (Standard C++ Logic)
        // NOTE: This runs on the IPC thread. Ensure your objects are thread-safe!
        std::string result;
        if (g_CustomDispatcher) {
            result = g_CustomDispatcher(commandJson);
        } else {
            result = DispatchCommand(commandJson);
        }
        
        // 2. Marshalling
        // We need to return a pointer that persists after this function returns, 
        // but acts as a 'transfer' to the bridge. 
        // Since the bridge copies it immediately, we can use a static/global buffer
        // protected or just overwritten.
        // For robustness in this MVP, we just use a global string to hold the data.
        g_LastResult = result; 
        
        return g_LastResult.c_str();
    }

    // -----------------------------------------------------------------------
    // IGNITE
    // -----------------------------------------------------------------------
    bool Ignite(const std::string& appName, const std::string& version, const std::string& customManifest) {
        try {
            std::cout << "[Rail SDK] Igniting '" << appName << "'..." << std::endl;

            // 1. Generate Manifest
            std::string jsonManifest;
            if (!customManifest.empty()) {
                jsonManifest = customManifest;
                std::cout << "[Rail SDK] Using Custom Manifest." << std::endl;
            } else {
                jsonManifest = GenerateManifest(appName);
            }
            
            // 2. Save Manifest to Disk (For Static Discovery)
            // Filename: Rail.manifest.json in CWD
            {
                std::ofstream outFile("Rail.manifest.json");
                if (outFile.is_open()) {
                    outFile << jsonManifest;
                    outFile.close();
                    std::cout << "[Rail SDK] Manifest saved to 'Rail.manifest.json'" << std::endl;
                } else {
                    std::cerr << "[Rail SDK] WARNING: Failed to save manifest file." << std::endl;
                }
            }

            // 3. Load RailBridge.dll
            g_hBridgeDll = LoadLibraryA("RailBridge.dll");
            if (!g_hBridgeDll) {
                std::cerr << "[Rail SDK] CRITICAL: Could not load RailBridge.dll. Error: " << GetLastError() << std::endl;
                std::cerr << "[Rail SDK] Ensure RailBridge.dll is in the same folder as this executable." << std::endl;
                return false;
            }

            // 4. Resolve Functions
            g_fnIgnite = (LiqIgniteFunc)GetProcAddress(g_hBridgeDll, "Rail_Ignite");
            g_fnDisconnect = (LiqDisconnectFunc)GetProcAddress(g_hBridgeDll, "Rail_Disconnect");

            if (!g_fnIgnite || !g_fnDisconnect) {
                std::cerr << "[Rail SDK] CRITICAL: Could not find exports in RailBridge.dll." << std::endl;
                return false;
            }

            // 5. Connect
            // appName acts as instanceId
            int result = g_fnIgnite(appName.c_str(), jsonManifest.c_str(), BridgeCallback);
            
            if (result != 0) {
                 std::cerr << "[Rail SDK] Connection failed with error code: " << result << std::endl;
                 return false;
            }

            std::cout << "[Rail SDK] Connected to Rail Network!" << std::endl;
            g_Connected = true;
            return true;
        } catch (const std::exception& e) {
            std::cerr << "[Rail SDK] Error during Ignite: " << e.what() << std::endl;
            return false;
        }
    }

    void RegisterInstanceInternal(const std::string& name, rttr::variant instance) {
        internal::InstanceRegistry::Register(name, instance);
        std::cout << "[Rail SDK] Instance Registered: " << name << std::endl;
    }

    void Disconnect() {
        if (g_Connected && g_fnDisconnect) {
            g_fnDisconnect();
        }
        g_Connected = false;
        if (g_hBridgeDll) {
            FreeLibrary(g_hBridgeDll);
            g_hBridgeDll = nullptr;
        }
        std::cout << "[Rail SDK] Disconnected." << std::endl;
    }

    bool IsConnected() {
        return g_Connected;
    }

    void ProcessEvents() {
        // Since we are dispatching directly on the callback thread, 
        // the main thread just needs to stay alive.
        // In a real game engine, we would queue commands here.
    }

    std::string DebugDispatch(const std::string& json) {
        return DispatchCommand(json);
    }

} // namespace rail


