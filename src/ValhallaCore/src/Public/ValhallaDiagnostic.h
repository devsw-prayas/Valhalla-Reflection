#pragma once
#include "ValhallaCompiler.h"

#if defined(_DEBUG) || defined(DEBUG)
#define VALHALLA_BUILD_DEBUG 1
#define VALHALLA_BUILD_RELEASE 0
#else
#define VALHALLA_BUILD_DEBUG 0
#define VALHALLA_BUILD_RELEASE 1
#endif

#if VALHALLA_BUILD_DEBUG

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
