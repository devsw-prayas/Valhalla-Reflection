# valhalla_reflect(<target> [MODULE_NAME <name>] [MODULE_ROOT <dir>] [OUTPUT_DIR <dir>])
# MODULE_NAME/OUTPUT_DIR must match the Build.cs (defaults: target name, "Generated"); MODULE_ROOT defaults to the
# target's source dir.

set_property(GLOBAL PROPERTY VALHALLA_REGISTERED_MODULES "")

function(valhalla_reflect TARGET)
    cmake_parse_arguments(VR "" "MODULE_NAME;MODULE_ROOT;OUTPUT_DIR" "" ${ARGN})
    if(NOT VR_MODULE_NAME)
        set(VR_MODULE_NAME ${TARGET})
    endif()
    if(NOT VR_MODULE_ROOT)
        get_target_property(VR_MODULE_ROOT ${TARGET} SOURCE_DIR)
    endif()
    if(NOT VR_OUTPUT_DIR)
        set(VR_OUTPUT_DIR Generated)
    endif()
    get_filename_component(VR_MODULE_ROOT ${VR_MODULE_ROOT} ABSOLUTE)

    set(generated_dir ${VR_MODULE_ROOT}/${VR_OUTPUT_DIR})
    set(unity ${generated_dir}/${VR_MODULE_NAME}.generated.cpp)

    # Placeholder so configure succeeds before the first codegen run.
    if(NOT EXISTS ${unity})
        file(WRITE ${unity} "// Placeholder; Valhalla-Gen replaces this on the first build.\n")
    endif()

    target_sources(${TARGET} PRIVATE ${unity})
    target_include_directories(${TARGET} PUBLIC ${generated_dir})
    target_link_libraries(${TARGET} PUBLIC ValhallaCore)
    add_dependencies(${TARGET} ValhallaCodegen)

    set_property(GLOBAL APPEND PROPERTY VALHALLA_REGISTERED_MODULES ${VR_MODULE_ROOT})
endfunction()

# Deferred to the end of configure so every valhalla_reflect() call is captured.
function(_valhalla_write_registered_modules)
    get_property(modules GLOBAL PROPERTY VALHALLA_REGISTERED_MODULES)
    if(modules)
        list(REMOVE_DUPLICATES modules)
    endif()
    string(JOIN "\n" content ${modules})
    file(CONFIGURE OUTPUT ${CMAKE_BINARY_DIR}/ValhallaRegisteredModules.txt CONTENT "${content}\n" @ONLY)
endfunction()
cmake_language(DEFER DIRECTORY ${CMAKE_SOURCE_DIR} CALL _valhalla_write_registered_modules)
