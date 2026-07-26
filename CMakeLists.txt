cmake_minimum_required(VERSION 3.20)
project(Valhalla VERSION 1.1.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

add_subdirectory(src/ValhallaCore)
add_subdirectory(src/Valhalla-Gen)
