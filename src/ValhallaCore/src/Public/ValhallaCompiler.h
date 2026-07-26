#pragma once

namespace Valhalla {
#if defined(_MSC_VER)
#define VALHALLA_COMPILER_MSVC 1
#else
#define VALHALLA_COMPILER_MSVC 0
#endif

#if defined(__clang__)
#define VALHALLA_COMPILER_CLANG 1
#else
#define VALHALLA_COMPILER_CLANG 0
#endif

#if defined(__GNUC__) && !defined(__clang__)
#define VALHALLA_COMPILER_GCC 1
#else
#define VALHALLA_COMPILER_GCC 0
#endif
}

#if VALHALLA_COMPILER_MSVC
#define VALHALLA_FORCEINLINE __forceinline
#define VALHALLA_NOINLINE    __declspec(noinline)
#elif VALHALLA_COMPILER_CLANG || VALHALLA_COMPILER_GCC
#define VALHALLA_FORCEINLINE inline __attribute__((always_inline))
#define VALHALLA_NOINLINE    __attribute__((noinline))
#else
#define VALHALLA_FORCEINLINE inline
#define VALHALLA_NOINLINE
#endif

#define VALHALLA_INLINE inline

#if VALHALLA_COMPILER_MSVC
#define VALHALLA_COMPILER_BARRIER() _ReadWriteBarrier()
#elif VALHALLA_COMPILER_CLANG || VALHALLA_COMPILER_GCC
#define VALHALLA_COMPILER_BARRIER() asm volatile("" ::: "memory")
#else
#define VALHALLA_COMPILER_BARRIER()
#endif

#if VALHALLA_COMPILER_MSVC
#define VALHALLA_OPTIMIZE_OFF __pragma(optimize("", off))
#define VALHALLA_OPTIMIZE_ON  __pragma(optimize("", on))
#elif VALHALLA_COMPILER_CLANG || VALHALLA_COMPILER_GCC
#define VALHALLA_OPTIMIZE_OFF _Pragma("clang optimize off")
#define VALHALLA_OPTIMIZE_ON  _Pragma("clang optimize on")
#else
#define VALHALLA_OPTIMIZE_OFF
#define VALHALLA_OPTIMIZE_ON
#endif

#if VALHALLA_COMPILER_CLANG || VALHALLA_COMPILER_GCC
#define VALHALLA_LIKELY(x)   __builtin_expect(!!(x), 1)
#define VALHALLA_UNLIKELY(x) __builtin_expect(!!(x), 0)
#else
#define VALHALLA_LIKELY(x)   (x)
#define VALHALLA_UNLIKELY(x) (x)
#endif

#if VALHALLA_COMPILER_MSVC
#define VALHALLA_DEBUG_BREAK() __debugbreak()
#define VALHALLA_TRAP()        __debugbreak()
#elif VALHALLA_COMPILER_CLANG || VALHALLA_COMPILER_GCC
#define VALHALLA_DEBUG_BREAK() __builtin_trap()
#define VALHALLA_TRAP()        __builtin_trap()
#else
#include <cstdlib>
#define VALHALLA_DEBUG_BREAK() std::abort()
#define VALHALLA_TRAP()        std::abort()
#endif

#if VALHALLA_COMPILER_MSVC
#define VALHALLA_UNREACHABLE() __assume(0)
#elif VALHALLA_COMPILER_CLANG || VALHALLA_COMPILER_GCC
#define VALHALLA_UNREACHABLE() __builtin_unreachable()
#else
#define VALHALLA_UNREACHABLE() VALHALLA_TRAP()
#endif

#if VALHALLA_COMPILER_MSVC
#define VALHALLA_PRAGMA(x) __pragma(x)
#elif VALHALLA_COMPILER_CLANG || VALHALLA_COMPILER_GCC
#define VALHALLA_PRAGMA(x) _Pragma(#x)
#else
#define VALHALLA_PRAGMA(x)
#endif

#define VALHALLA_DIAGNOSTIC_PUSH VALHALLA_PRAGMA(diagnostic push)
#define VALHALLA_DIAGNOSTIC_POP  VALHALLA_PRAGMA(diagnostic pop)

#if VALHALLA_COMPILER_MSVC
#define VALHALLA_DISABLE_WARNING(w) VALHALLA_PRAGMA(warning(disable : w))
#elif VALHALLA_COMPILER_CLANG || VALHALLA_COMPILER_GCC
#define VALHALLA_DISABLE_WARNING(w) VALHALLA_PRAGMA(clang diagnostic ignored w)
#else
#define VALHALLA_DISABLE_WARNING(w)
#endif

#if defined(__has_cpp_attribute)
#if __has_cpp_attribute(fallthrough)
#define VALHALLA_FALLTHROUGH [[fallthrough]]
#else
#define VALHALLA_FALLTHROUGH
#endif
#else
#define VALHALLA_FALLTHROUGH
#endif

#if defined(__has_cpp_attribute)
#if __has_cpp_attribute(nodiscard)
#define VALHALLA_NODISCARD [[nodiscard]]
#if __cplusplus >= 202002L
#define VALHALLA_NODISCARD_MSG(msg) [[nodiscard(msg)]]
#else
#define VALHALLA_NODISCARD_MSG(msg) [[nodiscard]]
#endif
#else
#define VALHALLA_NODISCARD
#define VALHALLA_NODISCARD_MSG(msg)
#endif
#else
#define VALHALLA_NODISCARD
#define VALHALLA_NODISCARD_MSG(msg)
#endif

#if defined(__has_cpp_attribute)
#if __has_cpp_attribute(maybe_unused)
#define VALHALLA_MAYBE_UNUSED [[maybe_unused]]
#else
#define VALHALLA_MAYBE_UNUSED
#endif
#else
#define VALHALLA_MAYBE_UNUSED
#endif

#if defined(__has_cpp_attribute)
#if __has_cpp_attribute(deprecated)
#define VALHALLA_DEPRECATED [[deprecated]]
#define VALHALLA_DEPRECATED_MSG(msg) [[deprecated(msg)]]
#else
#define VALHALLA_DEPRECATED
#define VALHALLA_DEPRECATED_MSG(msg)
#endif
#else
#define VALHALLA_DEPRECATED
#define VALHALLA_DEPRECATED_MSG(msg)
#endif

#if defined(__has_cpp_attribute)
#if __has_cpp_attribute(noreturn)
#define VALHALLA_NORETURN [[noreturn]]
#else
#define VALHALLA_NORETURN
#endif
#else
#define VALHALLA_NORETURN
#endif

#if VALHALLA_COMPILER_MSVC
#define VALHALLA_RESTRICT __restrict
#elif VALHALLA_COMPILER_CLANG || VALHALLA_COMPILER_GCC
#define VALHALLA_RESTRICT __restrict__
#else
#define VALHALLA_RESTRICT
#endif

#define VALHALLA_ALIGNAS(n) alignas(n)

#if VALHALLA_COMPILER_CLANG || VALHALLA_COMPILER_GCC
#define VALHALLA_ASSUME_ALIGNED(ptr, n) __builtin_assume_aligned((ptr), (n))
#else
#define VALHALLA_ASSUME_ALIGNED(ptr, n) (ptr)
#endif

#if VALHALLA_COMPILER_CLANG || VALHALLA_COMPILER_GCC
#define VALHALLA_HOT  __attribute__((hot))
#define VALHALLA_COLD __attribute__((cold))
#else
#define VALHALLA_HOT
#define VALHALLA_COLD
#endif
