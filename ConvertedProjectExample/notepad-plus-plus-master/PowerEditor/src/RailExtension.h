#pragma once

#include <string>
// #define NOMINMAX - Already defined in project props
#include <windows.h>
#include <functional>

class RailExtension {
public:
    static void Initialize(void* originalInstance);
    // Helper to dispatch commands to main thread if needed
    static void DispatchToMainThread(std::function<void()> func);
    static void Npp_New();
    static void Npp_Write(std::string text);
    static void Npp_Save(std::string filename);
};
