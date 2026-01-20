cmake_minimum_required(VERSION 3.14)
project(RailSDK)

# --- 1. FORZA ARCHITETTURA E RUNTIME ---
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Forziamo Static Runtime (/MT) in Release
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded")
set(CMAKE_CXX_FLAGS_RELEASE "${CMAKE_CXX_FLAGS_RELEASE} /MT")

# Disabilitiamo il prefisso "lib" su Windows per coerenza
set(CMAKE_SHARED_LIBRARY_PREFIX "")

include(FetchContent)

# --- 2. RTTR (STATIC + /MT) ---
FetchContent_Declare(
  rttr
  GIT_REPOSITORY https://github.com/rttrorg/rttr.git
  GIT_TAG        master
)
# Configurazione per inglobare RTTR
set(BUILD_STATIC ON CACHE BOOL "" FORCE)
set(BUILD_RTTR_DYNAMIC OFF CACHE BOOL "" FORCE)
set(BUILD_UNIT_TESTS OFF CACHE BOOL "" FORCE)
set(BUILD_EXAMPLES OFF CACHE BOOL "" FORCE)
set(USE_PCH OFF CACHE BOOL "" FORCE)
set(BUILD_WITH_STATIC_RUNTIME_LIBS ON CACHE BOOL "" FORCE)
set(BUILD_INSTALLER OFF CACHE BOOL "" FORCE)
set(BUILD_DOCUMENTATION OFF CACHE BOOL "" FORCE)
set(BUILD_PACKAGE OFF CACHE BOOL "" FORCE)

FetchContent_MakeAvailable(rttr)

# --- 3. NLOHMANN JSON ---
FetchContent_Declare(
  json
  GIT_REPOSITORY https://github.com/nlohmann/json.git
  GIT_TAG        v3.11.2
)
FetchContent_MakeAvailable(json)

# --- 4. BUILD ---
include_directories(${CMAKE_SOURCE_DIR}/include)
# RTTR includes
include_directories(${rttr_SOURCE_DIR}/src)
include_directories(${rttr_BINARY_DIR}/src)

file(GLOB_RECURSE SOURCES "src/*.cpp")

add_library(rail_sdk SHARED ${SOURCES})

# Definiamo l'export
target_compile_definitions(rail_sdk PRIVATE Rail_SDK_EXPORTS)

# Linkiamo tutto staticamente
target_link_libraries(rail_sdk 
    PRIVATE rttr_core_lib 
    PRIVATE nlohmann_json::nlohmann_json
)

# --- 5. INSTALL ---
include(GNUInstallDirs)
install(TARGETS rail_sdk RUNTIME DESTINATION bin LIBRARY DESTINATION lib ARCHIVE DESTINATION lib)
install(FILES "include/rail/rail.h" DESTINATION include/rail)
install(DIRECTORY "${json_SOURCE_DIR}/include/nlohmann" DESTINATION include)
