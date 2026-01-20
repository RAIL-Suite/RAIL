#include <string.h>
#include <stdio.h>
#include <stdlib.h>
#include <iostream>
#include <string>
#include <algorithm> // for tolower

#define Rail_NO_RTTR
#include "Rail.h"

// Doom Headers
extern "C" {
    #include "doomdef.h"
    #include "d_player.h"
    #include "g_game.h"
    #include "doomdata.h" // for ticcmd_t
    #include "doomstat.h" // for consoleplayer

    extern player_t players[MAXPLAYERS];
    extern int consoleplayer;
}

// ----------------------------------------------------------------------------
// AGENT STATE (Overrides)
// ----------------------------------------------------------------------------
struct AgentState {
    int move_tics;      // How many tics to keep moving
    int forward_move;   // Value to apply (MAXPLMOVE or negative)
    int side_move;      // Value to apply
    
    int turn_tics;      // How many tics to turn
    int angle_turn;     // Value to apply (per tic)
    
    int fire_tics;      // How many tics to hold fire button
    int use_tics;       // How many tics to hold use button
};

static AgentState g_agent = {0};

// Helper: Convert Ms to Tics (35 tics per second)
int MsToTics(int ms) {
    if (ms <= 0) return 0;
    return (int)((double)ms / 1000.0 * 35.0);
}

// ----------------------------------------------------------------------------
// HOOK: Called by G_BuildTiccmd in g_game.c
// ----------------------------------------------------------------------------
extern "C" void Rail_OverrideInput(ticcmd_t* cmd) {
    // 1. Movement
    if (g_agent.move_tics > 0) {
        cmd->forwardmove = g_agent.forward_move;
        cmd->sidemove = g_agent.side_move;
        g_agent.move_tics--;
    }

    // 2. Turning
    if (g_agent.turn_tics > 0) {
        cmd->angleturn = g_agent.angle_turn;
        g_agent.turn_tics--;
    }

    // 3. Firing
    if (g_agent.fire_tics > 0) {
        cmd->buttons |= BT_ATTACK;
        g_agent.fire_tics--;
    }

    // 4. Using (Open Doors)
    if (g_agent.use_tics > 0) {
        cmd->buttons |= BT_USE;
        g_agent.use_tics--;
    }
}

// ----------------------------------------------------------------------------
// DISPATCHER COMMANDS
// ----------------------------------------------------------------------------

void Doom_GodMode() {
    players[consoleplayer].cheats ^= CF_GODMODE;
    printf("Rail AGENT: Toggled God Mode for player %d\n", consoleplayer);
    players[consoleplayer].message = "Rail AGENT: GOD MODE TOGGLED";
}

void Doom_Move(const std::string& direction, int ms) {
    int tics = MsToTics(ms);
    g_agent.move_tics = tics;
    g_agent.forward_move = 0;
    g_agent.side_move = 0;

    std::string dir = direction;
    std::transform(dir.begin(), dir.end(), dir.begin(), ::tolower);

    if (dir == "forward") g_agent.forward_move = 0x32; // Standard speed 50
    else if (dir == "backward") g_agent.forward_move = -0x32;
    else if (dir == "left") g_agent.side_move = -0x28; // Standard strafe 40
    else if (dir == "right") g_agent.side_move = 0x28;
    
    printf("Rail: Moving %s for %d ms (%d tics)\n", direction.c_str(), ms, tics);
}

void Doom_Rotate(int degrees) {
    // In Doom, full circle is 0-FFFF (65536)
    // 360 degrees = 65536
    // 1 degree = ~182 units
    // But angleturn is a DELTA. 
    // We want to turn X degrees over Y time? 
    // Simplify: Rotate instantly (in one tic) is too fast visually.
    // Let's spread it over 10 tics (approx 300ms) for smoothness.
    
    int total_units = (int)(degrees * (65536.0 / 360.0));
    int duration_tics = 10; 
    
    g_agent.turn_tics = duration_tics;
    g_agent.angle_turn = total_units / duration_tics; // Apply fraction per tic
    
    printf("Rail: Rotating %d degrees\n", degrees);
}

void Doom_Shoot(int ms) {
    if (ms <= 0) ms = 300; // Default tap
    g_agent.fire_tics = MsToTics(ms);
    printf("Rail: Shooting\n");
}

void Doom_Use() {
    g_agent.use_tics = 5; // Hold for a few frames to ensure registration
    printf("Rail: Interaction (Use)\n");
}

// C++ Callback matching std::function<std::string(const std::string&)>
std::string DoomDispatch(const std::string& command_json) {
    // 1. Normalize parsing by converting input to lowercase
    std::string cmd_lower = command_json;
    std::transform(cmd_lower.begin(), cmd_lower.end(), cmd_lower.begin(), ::tolower);

    // Very basic manual parsing for C++03/11 compatibility
    
    if (cmd_lower.find("godmode") != std::string::npos) {
        Doom_GodMode();
        return "{\"result\": \"success\"}";
    }
    
    if (cmd_lower.find("move") != std::string::npos) {
        // Extract direction (default to Forward if parsing fails, but try hard to find others)
        std::string dir = "forward";
        
        if (cmd_lower.find("backward") != std::string::npos) dir = "backward";
        else if (cmd_lower.find("left") != std::string::npos) dir = "left";
        else if (cmd_lower.find("right") != std::string::npos) dir = "right";
        
        // Extract ms
        int ms = 1000;
        size_t ms_pos = cmd_lower.find("\"ms\"");
        if (ms_pos != std::string::npos) {
            size_t val_start = cmd_lower.find_first_of("0123456789", ms_pos);
            if (val_start != std::string::npos) {
                ms = atoi(cmd_lower.c_str() + val_start);
            }
        }
        
        Doom_Move(dir, ms);
        return "{\"result\": \"success\"}";
    }
    
    if (cmd_lower.find("rotate") != std::string::npos) {
        int deg = 0;
        size_t deg_pos = cmd_lower.find("\"degrees\"");
        if (deg_pos != std::string::npos) {
            size_t val_start = cmd_lower.find_first_of("-0123456789", deg_pos);
            if (val_start != std::string::npos) {
                deg = atoi(cmd_lower.c_str() + val_start);
            }
        }
        Doom_Rotate(deg);
        return "{\"result\": \"success\"}";
    }
    
    if (cmd_lower.find("shoot") != std::string::npos) {
        Doom_Shoot(300);
        return "{\"result\": \"success\"}";
    }

    if (cmd_lower.find("use") != std::string::npos) {
        Doom_Use();
        return "{\"result\": \"success\"}";
    }

    return "{\"error\": \"unknown command\"}";
}

extern "C" void Rail_DoomInit() {
    // Define the manifest for the AI
    std::string manifest = "{"
        "\"appName\": \"Doom\","
        "\"runtime_type\": \"dotnet-ipc\","
        "\"functions\": ["
            "{\"name\": \"Doom.GodMode\", \"description\": \"Toggles God Mode\", \"parameters\": [], \"return_type\": \"void\"},"
            "{\"name\": \"Doom.Move\", \"description\": \"Moves the player\", \"parameters\": [{\"name\":\"direction\",\"type\":\"string\"}, {\"name\":\"ms\",\"type\":\"integer\"}],"
            " \"return_type\": \"void\"},"
            "{\"name\": \"Doom.Rotate\", \"description\": \"Rotates view\", \"parameters\": [{\"name\":\"degrees\",\"type\":\"integer\"}], \"return_type\": \"void\"},"
            "{\"name\": \"Doom.Shoot\", \"description\": \"Fire weapon\", \"parameters\": [], \"return_type\": \"void\"},"
            "{\"name\": \"Doom.Use\", \"description\": \"Interact (Open Door)\", \"parameters\": [], \"return_type\": \"void\"}"
        "]"
    "}";
    
    // Connect to RailLLM
    rail::Ignite("Doom", "1.0.0", manifest);
    
    // Register the custom dispatcher
    rail::SetCustomDispatcher(DoomDispatch);
    
    printf("Rail AGENT: Doom Connected! (Remote Control Ready)\n");
}




