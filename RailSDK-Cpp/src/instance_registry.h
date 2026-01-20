#pragma once

#include <string>
#include <map>
#include <mutex>
#include <rttr/type>

namespace rail {
namespace internal {

    /**
     * @brief Thread-safe registry for live object instances.
     * Used to lookup objects by ID when invoking methods from JSON.
     */
    class InstanceRegistry {
    public:
        // Register an instance (thread-safe)
        static void Register(const std::string& id, rttr::variant instance);

        // Retrieve an instance (thread-safe)
        static rttr::variant Get(const std::string& id);

        // Remove an instance
        static void Unregister(const std::string& id);

        // Check if an instance exists
        static bool Contains(const std::string& id);

    private:
        static std::map<std::string, rttr::variant> _instances;
        static std::mutex _mutex;
    };

} // namespace internal
} // namespace rail


