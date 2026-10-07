cmake_minimum_required(VERSION 3.20)

# No vcpkg dependencies; opt out of a user-wide `vcpkg integrate install` (its post-build step needs pwsh).
if(NOT DEFINED CMAKE_VS_GLOBALS)
    set(CMAKE_VS_GLOBALS "VcpkgEnabled=false")
endif()
project(ValhallaCore LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

file(GLOB_RECURSE ValhallaCore_HEADERS  CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/src/Public/*.h")
file(GLOB_RECURSE ValhallaCore_SOURCE   CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/src/Private/*.cpp")
file(GLOB_RECURSE ValhallaCore_INL      CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/src/Public/*.inl")
file(GLOB_RECURSE ValhallaCore_DEF      CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/src/Public/*.def")
# X-macro tables, not linker module-definition files.
set_source_files_properties(${ValhallaCore_DEF} PROPERTIES HEADER_FILE_ONLY TRUE)

add_library(ValhallaCore SHARED
    ${ValhallaCore_HEADERS}
    ${ValhallaCore_SOURCE}
    ${ValhallaCore_INL}
    ${ValhallaCore_DEF}
)
target_compile_definitions(ValhallaCore PRIVATE ValhallaCore_EXPORTS)
target_include_directories(ValhallaCore PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/src/Public
)

set(VALHALLA_MAX_PARAMS 16 CACHE STRING "Maximum reflected method parameter count")
set(VALHALLA_ENABLE_ASSERT AUTO CACHE STRING "Runtime Invoke asserts: ON, OFF or AUTO (debug builds only)")
set_property(CACHE VALHALLA_ENABLE_ASSERT PROPERTY STRINGS AUTO ON OFF)

# AUTO follows _DEBUG (set by the debug CRT); ON/OFF pin it. Valhalla-scoped, so it never collides with a host's DEBUG.
set(VALHALLA_ENABLE_DEBUG_CHECKS AUTO CACHE STRING "Valhalla asserts: AUTO (follow _DEBUG), ON or OFF")
set_property(CACHE VALHALLA_ENABLE_DEBUG_CHECKS PROPERTY STRINGS AUTO ON OFF)
if (VALHALLA_ENABLE_DEBUG_CHECKS STREQUAL "ON")
    target_compile_definitions(ValhallaCore PUBLIC VALHALLA_DEBUG_CHECKS=1)
elseif (VALHALLA_ENABLE_DEBUG_CHECKS STREQUAL "OFF")
    target_compile_definitions(ValhallaCore PUBLIC VALHALLA_DEBUG_CHECKS=0)
endif()

target_compile_definitions(ValhallaCore PUBLIC VALHALLA_MAX_PARAMS=${VALHALLA_MAX_PARAMS})
if(VALHALLA_ENABLE_ASSERT STREQUAL "ON")
    target_compile_definitions(ValhallaCore PUBLIC VALHALLA_ENABLE_ASSERT=1)
elseif(VALHALLA_ENABLE_ASSERT STREQUAL "OFF")
    target_compile_definitions(ValhallaCore PUBLIC VALHALLA_ENABLE_ASSERT=0)
endif()

target_precompile_headers(ValhallaCore PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/src/Public/ValhallaCore.h)

target_compile_options(ValhallaCore PRIVATE
    $<$<AND:$<COMPILE_LANGUAGE:CXX>,$<CXX_COMPILER_FRONTEND_VARIANT:MSVC>>:/arch:AVX2 /W4 /permissive->
    $<$<AND:$<COMPILE_LANGUAGE:CXX>,$<NOT:$<CXX_COMPILER_FRONTEND_VARIANT:MSVC>>>:-mavx2 -Wall -Wextra>
)
