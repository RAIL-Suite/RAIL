#pragma once

#include <string>
#ifndef Rail_NO_RTTR
#include <rttr/type>
#endif
#include <functional>

// Gestione Export/Import per Windows DLL
#ifdef _WIN32
  #ifdef Rail_SDK_EXPORTS
    #define Rail_API __declspec(dllexport)
  #else
    #define Rail_API __declspec(dllimport)
  #endif
#else
  #define Rail_API
#endif

namespace rail {
    // Inizializza la connessione
    Rail_API bool Ignite(const std::string& appName, const std::string& version = "1.0.0", const std::string& customManifest = "");

    // Imposta un dispatcher personalizzato (per Legacy Clients senza RTTR)
    Rail_API void SetCustomDispatcher(std::function<std::string(const std::string&)> dispatcher);

#ifndef Rail_NO_RTTR
    // Registra istanza per l'esecuzione (Implementazione interna)
    Rail_API void RegisterInstanceInternal(const std::string& name, rttr::variant instance);

    // Template helper per il cliente (header-only)
    template<typename T>
    void RegisterInstance(const std::string& name, T instance) {
        RegisterInstanceInternal(name, instance);
    }
#endif

    Rail_API void Disconnect();
    Rail_API bool IsConnected();
    Rail_API void ProcessEvents();

    // Debug Helper: Dispatch JSON directly without pipe
    Rail_API std::string DebugDispatch(const std::string& json);
}


