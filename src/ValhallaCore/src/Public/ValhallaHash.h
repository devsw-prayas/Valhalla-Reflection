#pragma once
#include <cstdint>
#include <cstddef>

namespace Valhalla {

	// FNV-1a 64-bit, per addendum v1.1 §A.1. This exact constant pair (offset
	// basis + prime) and byte-at-a-time XOR-then-multiply order must match the
	// eventual C# implementation bit-for-bit - the CHD key is computed once by
	// Gen and once by ValhallaCore, and they only agree if both sides hash
	// identically. Do not "simplify" this without re-checking against the C#
	// side once it exists.
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

	// Reference test vectors (Fowler/Noll/Vo FNV-1a 64-bit) - if these ever fail,
	// the algorithm above has been altered and every hash it's ever produced is
	// now wrong relative to any C# implementation checked against the originals.
	static_assert(fnv1a("") == 0xcbf29ce484222325ULL, "FNV-1a 64-bit empty-string vector mismatch");
	static_assert(fnv1a("a") == 0xaf63dc4c8601ec8cULL, "FNV-1a 64-bit \"a\" vector mismatch");
	static_assert(fnv1a("foobar") == 0x85944171f73967e8ULL, "FNV-1a 64-bit \"foobar\" vector mismatch");

}
