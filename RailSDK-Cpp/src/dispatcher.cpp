#include <rail/Rail.h>
#include "instance_registry.h"
#include <rttr/type>
#include <rttr/registration>
#include <nlohmann/json.hpp>
#include <iostream>
#include <vector>
#include <sstream>

using json = nlohmann::json;

namespace rail {

    // Helper to convert nlohmann::json value to rttr::variant based on target type
    rttr::variant JsonToVariant(const json& jVal, const rttr::type& targetType) {
        if (targetType == rttr::type::get<int>()) {
            if (jVal.is_number_integer()) return jVal.get<int>();
            if (jVal.is_string()) return std::stoi(jVal.get<std::string>());
        }
        if (targetType == rttr::type::get<float>()) {
            if (jVal.is_number()) return jVal.get<float>();
            if (jVal.is_string()) return std::stof(jVal.get<std::string>());
        }
        if (targetType == rttr::type::get<double>()) {
            if (jVal.is_number()) return jVal.get<double>();
            if (jVal.is_string()) return std::stod(jVal.get<std::string>());
        }
        if (targetType == rttr::type::get<bool>()) {
            if (jVal.is_boolean()) return jVal.get<bool>();
            if (jVal.is_string()) return (jVal.get<std::string>() == "true");
        }
        if (targetType == rttr::type::get<std::string>()) {
            if (jVal.is_string()) return jVal.get<std::string>();
            return jVal.dump(); // Fallback: convert anything to string
        }
        return rttr::variant();
    }

    std::string DispatchCommand(const std::string& jsonCmd) {
        try {
            auto cmd = json::parse(jsonCmd);
            
            if (!cmd.contains("method")) {
                 return "{\"error\": \"Invalid JSON command structure: missing method\"}";
            }
            std::string methodNameFull = cmd["method"];
            std::string methodName = methodNameFull; // Default to full, refine later

            // 1. Validate Command Structure & Context
            std::string context;
            
            // PRIORITY 1: Explicit 'class' or 'context' field
            if (cmd.contains("class") && !cmd["class"].is_null()) {
                context = cmd["class"];
            } else if (cmd.contains("context")) {
                context = cmd["context"];
            } else {
                // PRIORITY 2: Implicit Context in Method Name (Format: "Context.Method")
                size_t dotPos = methodNameFull.rfind('.');
                if (dotPos != std::string::npos) {
                    context = methodNameFull.substr(0, dotPos);
                    methodName = methodNameFull.substr(dotPos + 1); // We found the split, so update method name here
                } else {
                     return "{\"error\": \"Invalid JSON command structure: missing class or context, and method name '" + methodNameFull + "' has no dot separator.\"}";
                }
            }
            
            // 2. Find Instance
            rttr::variant instance = internal::InstanceRegistry::Get(context);
            if (!instance.is_valid()) {
                return "{\"error\": \"Instance not found: " + context + "\"}";
            }

            // 3. Resolve Method Name (If not already split above)
            // If we had explicit context, we might still have received "OrderManager.CreateOrder" as method.
            // So we strip the prefix if it matches the context OR if it's just a dot separator.
            if (methodName == methodNameFull) { 
                 size_t dotPos = methodName.rfind('.');
                 if (dotPos != std::string::npos) {
                     methodName = methodName.substr(dotPos + 1);
                 }
            }

            // 4. Find Method via RTTR
            rttr::type type = instance.get_type();
            
            // Fix: Unwrap pointers and wrappers
            if (type.is_pointer()) {
                type = type.get_raw_type(); 
            } else if (type.is_wrapper()) {
                type = type.get_wrapped_type();
                instance = instance.extract_wrapped_value();
            }
            
            rttr::method method = type.get_method(methodName);
            
            if (!method.is_valid()) {
                 return "{\"error\": \"Method not found: " + methodName + " on type " + type.get_name().to_string() + "\"}";
            }

            // 5. Build Arguments (Named or Positional)
            auto paramInfos = method.get_parameter_infos();
            
            std::vector<rttr::variant> argsVariants; 
            argsVariants.resize(paramInfos.size()); 

            if (cmd.contains("args")) {
                const auto& jArgs = cmd["args"];
                if (jArgs.is_array()) {
                    // Positional Handling
                    size_t i = 0;
                    for (const auto& param : paramInfos) {
                        if (i < jArgs.size()) {
                             argsVariants[i] = JsonToVariant(jArgs[i], param.get_type());
                        }
                        i++;
                    }
                } else if (jArgs.is_object()) {
                    // Named Handling (RailLLM Host Default)
                    size_t i = 0;
                    for (const auto& param : paramInfos) {
                        std::string pName = param.get_name().to_string();
                        // RTTR param names: match against JSON keys
                        if (jArgs.contains(pName)) {
                            argsVariants[i] = JsonToVariant(jArgs[pName], param.get_type());
                        }
                        i++;
                    }
                }
            }

            // 6. Invoke
            rttr::variant result;
            size_t argCount = argsVariants.size();

             switch (argCount) {
                 case 0: result = method.invoke(instance); break;
                 case 1: result = method.invoke(instance, argsVariants[0]); break;
                 case 2: result = method.invoke(instance, argsVariants[0], argsVariants[1]); break;
                 case 3: result = method.invoke(instance, argsVariants[0], argsVariants[1], argsVariants[2]); break;
                 case 4: result = method.invoke(instance, argsVariants[0], argsVariants[1], argsVariants[2], argsVariants[3]); break;
                 case 5: result = method.invoke(instance, argsVariants[0], argsVariants[1], argsVariants[2], argsVariants[3], argsVariants[4]); break;
                 case 6: result = method.invoke(instance, argsVariants[0], argsVariants[1], argsVariants[2], argsVariants[3], argsVariants[4], argsVariants[5]); break;
                 default: 
                     return "{\"error\": \"Too many arguments (max 6 supported)\"}";
             }
            
            if (!result.is_valid()) {
                 if (method.get_return_type() == rttr::type::get<void>()) {
                     return "{\"result\": \"void\"}";
                 }
                 return "{\"error\": \"Invocation failed (returned invalid variant)\"}";
            }

            // 7. Return Result
            std::string resStr = result.to_string(); 
            json resJson;
            resJson["result"] = resStr; 
            return resJson.dump();

        } catch (const json::parse_error& e) {
            return "{\"error\": \"JSON parse error: " + std::string(e.what()) + "\"}";
        } catch (const std::exception& e) {
            return "{\"error\": \"Dispatch exception: " + std::string(e.what()) + "\"}";
        } catch (...) {
            return "{\"error\": \"Unknown dispatch error\"}";
        }
    }

} // namespace rail


