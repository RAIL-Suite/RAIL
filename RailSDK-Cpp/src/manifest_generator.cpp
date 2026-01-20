#include <rttr/type>
#include <iostream>
#include <string>
#include <vector>
#include <sstream>
#include <iomanip>

namespace rail {

    // Helper per escape JSON string
    std::string EscapeJson(const std::string& s) {
        std::ostringstream o;
        for (auto c : s) {
            if (c == '"') o << "\\\"";
            else if (c == '\\') o << "\\\\";
            else if ('\x00' <= c && c <= '\x1f') o << "\\u" << std::hex << std::setw(4) << std::setfill('0') << (int)c;
            else o << c;
        }
        return o.str();
    }

    std::string GenerateManifest(const std::string& appName) {
        std::ostringstream json;
        json << "{";
        json << "\"language\": \"cpp\",";
        json << "\"appName\": \"" << EscapeJson(appName) << "\",";
        json << "\"functions\": [";

        auto types = rttr::type::get_types();
        bool firstFunc = true;

        for (const auto& t : types) {
            // FIX TYPO: is_class(), non is_clss()
            if (!t.is_class()) continue; 

            std::string className = t.get_name().to_string();

            for (const auto& meth : t.get_methods()) {
                if (!firstFunc) json << ",";
                firstFunc = false;

                std::string methodName = meth.get_name().to_string();
                
                json << "{";
                json << "\"name\": \"" << EscapeJson(className + "." + methodName) << "\",";
                
                // FIX METADATA: get_metadata torna un rttr::variant
                rttr::variant descVar = meth.get_metadata("description");
                std::string desc = descVar.is_valid() ? descVar.to_string() : "";
                json << "\"description\": \"" << EscapeJson(desc) << "\",";

                json << "\"parameters\": [";
                auto params = meth.get_parameter_infos();
                bool firstParam = true;
                for (const auto& p : params) {
                    if (!firstParam) json << ",";
                    firstParam = false;
                    
                    json << "{";
                    json << "\"name\": \"" << EscapeJson(p.get_name().to_string()) << "\",";
                    json << "\"type\": \"" << EscapeJson(p.get_type().get_name().to_string()) << "\"";
                    json << "}";
                }
                json << "]"; // end parameters
                json << "}"; // end method
            }
        }

        json << "]"; // end functions
        json << "}"; // end root
        return json.str();
    }
}


