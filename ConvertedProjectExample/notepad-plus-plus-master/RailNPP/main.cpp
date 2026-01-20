#include <windows.h>
#include <string>
#include <vector>
#include <functional>
#include "PluginInterface.h"
#include "Scintilla.h"
#include "Notepad_plus_msgs.h"
#include "menuCmdID.h"
#include <rail/rail.h>
#include <nlohmann/json.hpp>

// Globals
NppData nppData;
HANDLE hModule;
using json = nlohmann::json;

// --- Rail Integration ---

// Forward declarations
void Npp_New();
void Npp_Write(const std::string& text);
void Npp_Save(const std::string& filename);
std::string Npp_GetSelection();
void Npp_ReplaceSelection(const std::string& text);

// Dispatcher
std::string ManualDispatch(const std::string& cmdJson) {
    try {
        auto j = json::parse(cmdJson);
        std::string method;
        if (j.contains("method")) method = j["method"];
        else return "{\"error\":\"No method specified\"}";

        // Normalize method name "Notepad.Write" -> "Write"
        size_t dot = method.find('.');
        if (dot != std::string::npos) method = method.substr(dot + 1);

        if (method == "Npp_New" || method == "fileNew" || method == "New" || method == "Notepad.fileNew") {
            Npp_New();
            return "{\"result\":\"Success\"}";
        }
        if (method == "Npp_Write" || method == "writeText" || method == "Write" || method == "Notepad.writeText") {
            std::string text = "";
            if (j.contains("args")) {
                auto& args = j["args"];
                if (args.is_object() && args.contains("text")) {
                    auto& t = args["text"];
                    if (t.is_string()) text = t.get<std::string>();
                    else text = t.dump();
                } else if (args.is_array() && !args.empty()) {
                    if (args[0].is_string()) text = args[0].get<std::string>();
                }
            }
            Npp_Write(text);
            return "{\"result\":\"Success\"}";
        }
        if (method == "Npp_Save" || method == "saveFile" || method == "Save" || method == "Notepad.saveFile") {
            std::string filename = "";
            if (j.contains("args")) {
                auto& args = j["args"];
                if (args.is_object() && args.contains("filename")) {
                    filename = args["filename"].get<std::string>();
                } else if (args.is_array() && !args.empty()) {
                    filename = args[0].get<std::string>();
                }
            }
            Npp_Save(filename);
            return "{\"result\":\"Success\"}";
        }
        if (method == "Npp_GetSelection" || method == "getSelectedText" || method == "GetSelectedText") {
            std::string sel = Npp_GetSelection();
            json result;
            result["result"] = sel;
            return result.dump();
        }
        if (method == "Npp_ReplaceSelection" || method == "replaceSelection" || method == "ReplaceSelection") {
            std::string text = "";
            if (j.contains("args")) {
                auto& args = j["args"];
                if (args.is_object() && args.contains("text")) {
                    auto& t = args["text"];
                    if (t.is_string()) text = t.get<std::string>();
                    else text = t.dump();
                } else if (args.is_array() && !args.empty()) {
                    if (args[0].is_string()) text = args[0].get<std::string>();
                }
            }
            Npp_ReplaceSelection(text);
            return "{\"result\":\"Success\"}";
        }
        
        return "{\"error\":\"Method not found: " + method + "\"}";
    } catch (const std::exception& e) {
        return "{\"error\":\"Dispatch Exception: " + std::string(e.what()) + "\"}";    
    }
}

// Implementations
void Npp_New() {
    ::SendMessage(nppData._nppHandle, WM_COMMAND, IDM_FILE_NEW, 0);
}

void Npp_Write(const std::string& text) {
    // Get current Scintilla handle
    int which = -1;
    ::SendMessage(nppData._nppHandle, NPPM_GETCURRENTSCINTILLA, 0, (LPARAM)&which);
    HWND hSci = (which == 0) ? nppData._scintillaMainHandle : nppData._scintillaSecondHandle;
    
    // Append text
    ::SendMessage(hSci, SCI_ADDTEXT, text.length(), (LPARAM)text.c_str());
}

void Npp_Save(const std::string& filename) {
    if (!filename.empty()) {
        ::SendMessage(nppData._nppHandle, WM_COMMAND, IDM_FILE_SAVE, 0);
    } else {
        ::SendMessage(nppData._nppHandle, WM_COMMAND, IDM_FILE_SAVE, 0);
    }
}

std::string Npp_GetSelection() {
    int which = -1;
    ::SendMessage(nppData._nppHandle, NPPM_GETCURRENTSCINTILLA, 0, (LPARAM)&which);
    HWND hSci = (which == 0) ? nppData._scintillaMainHandle : nppData._scintillaSecondHandle;

    // Get length of selection (including null terminator)
    int len = (int)::SendMessage(hSci, SCI_GETSELTEXT, 0, 0);
    if (len <= 1) return "";

    std::vector<char> buffer(len);
    ::SendMessage(hSci, SCI_GETSELTEXT, 0, (LPARAM)buffer.data());
    return std::string(buffer.data());
}

void Npp_ReplaceSelection(const std::string& text) {
    int which = -1;
    ::SendMessage(nppData._nppHandle, NPPM_GETCURRENTSCINTILLA, 0, (LPARAM)&which);
    HWND hSci = (which == 0) ? nppData._scintillaMainHandle : nppData._scintillaSecondHandle;

    ::SendMessage(hSci, SCI_REPLACESEL, 0, (LPARAM)text.c_str());
}


// --- Plugin Exports ---

extern "C" __declspec(dllexport) void setInfo(NppData notpadPlusData) {
    nppData = notpadPlusData;
    
    // Define Custom Manifest to match RailLLM Tool Definition
    std::string customManifest = R"({
        "language": "cpp",
        "appName": "Notepad",
        "functions": [
            {
                "name": "Notepad.fileNew",
                "parameters": [],
                "return_type": "void"
            },
            {
                "name": "Notepad.writeText",
                "parameters": [ { "name": "text", "type": "string" } ],
                "return_type": "void"
            },
            {
                "name": "Notepad.saveFile",
                "parameters": [ { "name": "filename", "type": "string" } ],
                "return_type": "void"
            },
            {
                "name": "Notepad.getSelectedText",
                "parameters": [],
                "return_type": "string"
            },
            {
                "name": "Notepad.replaceSelection",
                "parameters": [ { "name": "text", "type": "string" } ],
                "return_type": "void"
            }
        ]
    })";

    // Initialize Rail with Custom Manifest
    rail::Ignite("Notepad", "1.0.0", customManifest);
    rail::SetCustomDispatcher(ManualDispatch);
}

extern "C" __declspec(dllexport) const TCHAR * getName() {
    return TEXT("RailNPP");
}

// About command
void About() {
    ::MessageBox(nppData._nppHandle, L"RailNPP loads RailSDK for Notepad++ agentic integration.", L"About RailNPP", MB_OK);
}

// Plugin Menu Items
FuncItem funcItem[1];

extern "C" __declspec(dllexport) FuncItem * getFuncsArray(int *nbF) {
    *nbF = 1;
    
    // Define "About RailNPP"
    wcscpy_s(funcItem[0]._itemName, menuItemSize, L"About RailNPP");
    funcItem[0]._pFunc = About;
    funcItem[0]._cmdID = 0;
    funcItem[0]._init2Check = false;
    funcItem[0]._pShKey = nullptr;

    return funcItem;
}

extern "C" __declspec(dllexport) void beNotified(SCNotification *notifyCode) {
    // Handle events if needed
}

extern "C" __declspec(dllexport) LRESULT messageProc(UINT Message, WPARAM wParam, LPARAM lParam) {
    return TRUE;
}

extern "C" __declspec(dllexport) BOOL isUnicode() {
    return TRUE;
}

BOOL APIENTRY DllMain( HANDLE hModule, 
                       DWORD  ul_reason_for_call, 
                       LPVOID lpReserved
					 ) {
    return TRUE;
}
