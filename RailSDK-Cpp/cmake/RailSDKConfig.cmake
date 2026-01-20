# ============================================================================
# Liquid SDK CMake Integration
# ============================================================================
# Provides liquid_generate_bindings() for automatic C++ method discovery.
#
# Usage:
#     find_package(LiquidSDK REQUIRED)
#     liquid_generate_bindings(TARGET myapp HEADERS include/*.h)
#
# ============================================================================

cmake_minimum_required(VERSION 3.16)

# Find the binding generator script
set(LIQUID_BINDGEN_SCRIPT "${CMAKE_CURRENT_LIST_DIR}/../tools/liquid_bindgen.py")

# ============================================================================
# liquid_generate_bindings
# ============================================================================
# Generates C++ binding code from header files.
#
# Arguments:
#   TARGET  - Target name to add generated sources to
#   HEADERS - List of header files to scan
#   OUTPUT  - (Optional) Output file path [default: ${CMAKE_BINARY_DIR}/liquid_bindings_generated.cpp]
#
function(liquid_generate_bindings)
    cmake_parse_arguments(PARSED "" "TARGET;OUTPUT" "HEADERS" ${ARGN})
    
    if(NOT PARSED_TARGET)
        message(FATAL_ERROR "liquid_generate_bindings: TARGET is required")
    endif()
    
    if(NOT PARSED_HEADERS)
        message(FATAL_ERROR "liquid_generate_bindings: HEADERS is required")
    endif()
    
    # Default output path
    if(NOT PARSED_OUTPUT)
        set(PARSED_OUTPUT "${CMAKE_BINARY_DIR}/liquid_bindings_generated.cpp")
    endif()
    
    # Collect header files
    set(HEADER_FILES)
    foreach(pattern ${PARSED_HEADERS})
        file(GLOB matched_files ${pattern})
        list(APPEND HEADER_FILES ${matched_files})
    endforeach()
    
    # Check if generator exists
    if(NOT EXISTS ${LIQUID_BINDGEN_SCRIPT})
        message(WARNING "Liquid binding generator not found at ${LIQUID_BINDGEN_SCRIPT}")
        message(WARNING "Using fallback empty bindings file")
        
        # Create empty bindings file
        file(WRITE ${PARSED_OUTPUT} "// Auto-generated Liquid bindings (empty fallback)\n")
        file(APPEND ${PARSED_OUTPUT} "#include <liquid/liquid.h>\n")
        file(APPEND ${PARSED_OUTPUT} "void liquid_register_all() {}\n")
    else()
        # Generate bindings using Python script
        add_custom_command(
            OUTPUT ${PARSED_OUTPUT}
            COMMAND python3 ${LIQUID_BINDGEN_SCRIPT}
                --headers ${HEADER_FILES}
                --output ${PARSED_OUTPUT}
            DEPENDS ${HEADER_FILES} ${LIQUID_BINDGEN_SCRIPT}
            COMMENT "Generating Liquid bindings for ${PARSED_TARGET}"
            VERBATIM
        )
    endif()
    
    # Add generated source to target
    target_sources(${PARSED_TARGET} PRIVATE ${PARSED_OUTPUT})
    
    # Link with Liquid SDK
    target_link_libraries(${PARSED_TARGET} PRIVATE LiquidSDK::liquid)
    
endfunction()
