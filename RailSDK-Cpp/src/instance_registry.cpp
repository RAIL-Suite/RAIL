#include "instance_registry.h"

namespace rail {
namespace internal {

    std::map<std::string, rttr::variant> InstanceRegistry::_instances;
    std::mutex InstanceRegistry::_mutex;

    void InstanceRegistry::Register(const std::string& id, rttr::variant instance) {
        std::lock_guard<std::mutex> lock(_mutex);
        _instances[id] = instance;
    }

    rttr::variant InstanceRegistry::Get(const std::string& id) {
        std::lock_guard<std::mutex> lock(_mutex);
        auto it = _instances.find(id);
        if (it != _instances.end()) {
            return it->second;
        }
        return rttr::variant(); // Invalid variant
    }

    void InstanceRegistry::Unregister(const std::string& id) {
        std::lock_guard<std::mutex> lock(_mutex);
        _instances.erase(id);
    }

    bool InstanceRegistry::Contains(const std::string& id) {
        std::lock_guard<std::mutex> lock(_mutex);
        return _instances.find(id) != _instances.end();
    }

} // namespace internal
} // namespace rail


