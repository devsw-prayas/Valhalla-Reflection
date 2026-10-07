#pragma once
#include "ValhallaCompiler.h"

// VALHALLA_DEBUG_CHECKS (0/1) comes from VALHALLA_ENABLE_DEBUG_CHECKS=ON/OFF; unset (AUTO) follows _DEBUG.
#if defined(VALHALLA_DEBUG_CHECKS)
#define VALHALLA_BUILD_DEBUG VALHALLA_DEBUG_CHECKS
#elif defined(_DEBUG)
#define VALHALLA_BUILD_DEBUG 1
#else
#define VALHALLA_BUILD_DEBUG 0
#endif
#define VALHALLA_BUILD_RELEASE (!VALHALLA_BUILD_DEBUG)

#ifndef VALHALLA_ENABLE_ASSERT
#define VALHALLA_ENABLE_ASSERT VALHALLA_BUILD_DEBUG
#endif

#if VALHALLA_ENABLE_ASSERT

#define VALHALLA_ASSERT(expr)                                     \
        do {                                                   \
            if (!(expr)) {                                    \
                VALHALLA_DEBUG_BREAK();                              \
                VALHALLA_TRAP();                                     \
            }                                                  \
        } while (0)

#else

#define VALHALLA_ASSERT(expr) do { (void)sizeof(expr); } while (0)

#endif

#if VALHALLA_BUILD_DEBUG
#define VALHALLA_ASSUME(expr) VALHALLA_ASSERT(expr)
#else
#if VALHALLA_COMPILER_MSVC
#define VALHALLA_ASSUME(expr) __assume(expr)
#elif VALHALLA_COMPILER_CLANG || VALHALLA_COMPILER_GCC
#define VALHALLA_ASSUME(expr) do { if (!(expr)) __builtin_unreachable(); } while (0)
#else
#define VALHALLA_ASSUME(expr) do { } while (0)
#endif
#endif

#if VALHALLA_BUILD_DEBUG
#define VALHALLA_DEBUG_ASSERT(expr) VALHALLA_ASSERT(expr)
#define VALHALLA_DEBUG_ASSUME(expr) VALHALLA_ASSUME(expr)
#else
#define VALHALLA_DEBUG_ASSERT(expr) do {} while (0)
#define VALHALLA_DEBUG_ASSUME(expr) do {} while (0)
#endif

#define VALHALLA_STATIC_ASSERT(expr, msg) static_assert(expr, msg)

#define VALHALLA_UNUSED(x) (void)(x)
