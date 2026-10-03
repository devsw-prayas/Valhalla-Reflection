cmake_minimum_required(VERSION 3.20)

# No vcpkg dependencies; opt out of a user-wide `vcpkg integrate install` (its post-build step needs pwsh).
if(NOT DEFINED CMAKE_VS_GLOBALS)
    set(CMAKE_VS_GLOBALS "VcpkgEnabled=false")
endif()
project(Valhalla VERSION 1.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

if(CMAKE_SOURCE_DIR STREQUAL CMAKE_CURRENT_SOURCE_DIR)
    set(VALHALLA_IS_TOP_LEVEL ON)
    set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
    set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
else()
    set(VALHALLA_IS_TOP_LEVEL OFF)
endif()

option(VALHALLA_BUILD_TESTS "Build the Valhalla smoke test" ${VALHALLA_IS_TOP_LEVEL})

add_subdirectory(src/ValhallaCore)
add_subdirectory(src/Valhalla-Gen)
include(cmake/ValhallaReflect.cmake)

if(VALHALLA_BUILD_TESTS)
    enable_testing()
    add_subdirectory(tests/ValhallaSmoke)
endif()
