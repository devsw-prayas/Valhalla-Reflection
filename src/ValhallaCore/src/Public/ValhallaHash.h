#pragma once
#include <cstdint>
#include <cstddef>

namespace Valhalla {

	// Must match the C# Gen side bit-for-bit — CHD keys depend on identical hashing.
	constexpr uint64_t kFnv1a64OffsetBasis = 0xcbf29ce484222325ULL;
	constexpr uint64_t kFnv1a64Prime = 0x100000001b3ULL;

	constexpr uint64_t fnv1a(const char* p_Str) noexcept {
		uint64_t hash = kFnv1a64OffsetBasis;
		for (const char* c = p_Str; *c != '\0'; ++c) {
			hash ^= static_cast<uint64_t>(static_cast<unsigned char>(*c));
			hash *= kFnv1a64Prime;
		}
		return hash;
	}

	constexpr uint64_t fnv1a(const char* p_Data, size_t v_Length) noexcept {
		uint64_t hash = kFnv1a64OffsetBasis;
		for (size_t i = 0; i < v_Length; ++i) {
			hash ^= static_cast<uint64_t>(static_cast<unsigned char>(p_Data[i]));
			hash *= kFnv1a64Prime;
		}
		return hash;
	}

	static_assert(fnv1a("") == 0xcbf29ce484222325ULL, "FNV-1a 64-bit empty-string vector mismatch");
	static_assert(fnv1a("a") == 0xaf63dc4c8601ec8cULL, "FNV-1a 64-bit \"a\" vector mismatch");
	static_assert(fnv1a("foobar") == 0x85944171f73967e8ULL, "FNV-1a 64-bit \"foobar\" vector mismatch");

}
