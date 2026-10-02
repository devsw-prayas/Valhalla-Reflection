add_executable(ValhallaSmoke
    Source/Public/SmokeTypes.h
    Source/Private/SmokeMain.cpp
)
target_include_directories(ValhallaSmoke PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/Source/Public)
target_compile_options(ValhallaSmoke PRIVATE
    $<$<CXX_COMPILER_FRONTEND_VARIANT:MSVC>:/W4 /permissive->
    $<$<NOT:$<CXX_COMPILER_FRONTEND_VARIANT:MSVC>>:-Wall -Wextra>
)
valhalla_reflect(ValhallaSmoke)
add_test(NAME ValhallaSmoke COMMAND ValhallaSmoke)
