find_program(DOTNET_EXECUTABLE dotnet REQUIRED)

set(VALHALLAGEN_CSPROJ ${CMAKE_CURRENT_SOURCE_DIR}/ValhallaGen.csproj)
set(VALHALLAGEN_OUTPUT_DIR ${CMAKE_CURRENT_SOURCE_DIR}/bin/Release)
set(VALHALLAGEN_DLL ${VALHALLAGEN_OUTPUT_DIR}/ValhallaGen.dll)
set(VALHALLA_PRIMITIVES_DEF ${CMAKE_CURRENT_SOURCE_DIR}/../ValhallaCore/src/Public/ValhallaPrimitives.def)

file(GLOB_RECURSE VALHALLAGEN_SOURCES CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/*.cs")
list(FILTER VALHALLAGEN_SOURCES EXCLUDE REGEX "/(bin|obj)/")

add_custom_command(
    OUTPUT ${VALHALLAGEN_DLL}
    COMMAND ${DOTNET_EXECUTABLE} build ${VALHALLAGEN_CSPROJ} -c Release -o ${VALHALLAGEN_OUTPUT_DIR}
    DEPENDS ${VALHALLAGEN_SOURCES} ${VALHALLAGEN_CSPROJ}
    WORKING_DIRECTORY ${CMAKE_CURRENT_SOURCE_DIR}
    COMMENT "Compiling Valhalla-Gen (dotnet build)"
    VERBATIM
)

add_custom_target(Valhalla-Gen ALL DEPENDS ${VALHALLAGEN_DLL})

set(VALHALLA_CODEGEN_ARGS --root ${CMAKE_SOURCE_DIR} --primitives ${VALHALLA_PRIMITIVES_DEF}
    --registered ${CMAKE_BINARY_DIR}/ValhallaRegisteredModules.txt)

# No OUTPUT: runs every build; Gen only rewrites files whose content changed.
add_custom_target(ValhallaCodegen ALL
    COMMAND ${DOTNET_EXECUTABLE} ${VALHALLAGEN_DLL} ${VALHALLA_CODEGEN_ARGS}
    WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
    COMMENT "Running Valhalla-Gen codegen"
    VERBATIM
)
add_dependencies(ValhallaCodegen Valhalla-Gen)
