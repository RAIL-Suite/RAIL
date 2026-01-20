#include <rttr/registration>
#include "legacy_code/OrderManager.h"

// ═══════════════════════════════════════════════════════════════
// Rail BINDING LAYER - SEPARATE FILE
// ═══════════════════════════════════════════════════════════════

// ForceLink pattern: Call this from main() to prevent linker stripping
void ForceLink_OrderManager() {} 

RTTR_REGISTRATION {
    rttr::registration::class_<OrderManager>("OrderManager")
        .constructor<>()
        .method("CreateOrder", &OrderManager::CreateOrder)
        (
            rttr::metadata("description", "Creates a new order for a client"),
            rttr::parameter_names("clientName", "quantity")
        )
        .method("GetOrderCount", &OrderManager::GetOrderCount)
        (
            rttr::metadata("description", "Returns total number of active orders")
        )
        .method("CancelOrder", &OrderManager::CancelOrder)
        (
            rttr::metadata("description", "Cancels an existing order by ID"),
            rttr::parameter_names("orderId")
        );
}


