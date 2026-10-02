#pragma once
#include <cstdint>
#include <cstddef>

namespace Valhalla::Chd {

	// Lookup side must match Valhalla-Gen's Chd.cs bit-for-bit; every generated module static_asserts its own slots against this.
	constexpr uint64_t kBucketSeed = 0x243f6a8885a308d3ULL;
	constexpr uint64_t kSlotSeed = 0x13198a2e03707344ULL;
	constexpr uint64_t kDisplacementStep = 0x9e3779b97f4a7c15ULL;

	constexpr uint64_t mix(uint64_t v_Value) noexcept {
		v_Value ^= v_Value >> 30;
		v_Value *= 0xbf58476d1ce4e5b9ULL;
		v_Value ^= v_Value >> 27;
		v_Value *= 0x94d049bb133111ebULL;
		v_Value ^= v_Value >> 31;
		return v_Value;
	}

	constexpr uint32_t bucketOf(uint64_t v_Key, uint32_t v_BucketCount) noexcept {
		return static_cast<uint32_t>(mix(v_Key ^ kBucketSeed) % v_BucketCount);
	}

	constexpr uint32_t slotOf(uint64_t v_Key, uint32_t v_Displacement, uint32_t v_SlotCount) noexcept {
		return static_cast<uint32_t>(mix(v_Key ^ (kSlotSeed + v_Displacement * kDisplacementStep)) % v_SlotCount);
	}

	constexpr uint32_t kEmptySlot = 0xffffffffu;

	template <size_t N>
	struct BuildResult {
		uint32_t m_BucketCount = 0;
		uint32_t m_SlotCount = 0;
		uint32_t m_Displacements[N] = {};
		uint32_t m_SlotToKey[N * 2] = {};
		bool m_Ok = false;
	};

	// Compile-time builder, used only for ValhallaCore's own fixed primitive table; Gen builds every other table offline.
	template <size_t N>
	consteval BuildResult<N> build(const uint64_t (&r_Keys)[N]) {
		BuildResult<N> result{};
		const uint32_t bucketCount = static_cast<uint32_t>(N / 2 + 1);

		for (uint32_t slotCount = static_cast<uint32_t>(N); slotCount <= N * 2; ++slotCount) {
			uint32_t bucketSizes[N / 2 + 1] = {};
			for (size_t i = 0; i < N; ++i) ++bucketSizes[bucketOf(r_Keys[i], bucketCount)];

			uint32_t order[N / 2 + 1] = {};
			for (uint32_t b = 0; b < bucketCount; ++b) order[b] = b;
			for (uint32_t a = 0; a < bucketCount; ++a)
				for (uint32_t b = a + 1; b < bucketCount; ++b)
					if (bucketSizes[order[b]] > bucketSizes[order[a]]) {
						const uint32_t tmp = order[a];
						order[a] = order[b];
						order[b] = tmp;
					}

			for (uint32_t s = 0; s < N * 2; ++s) result.m_SlotToKey[s] = kEmptySlot;
			bool ok = true;

			for (uint32_t o = 0; o < bucketCount && ok; ++o) {
				const uint32_t bucket = order[o];
				if (bucketSizes[bucket] == 0) {
					result.m_Displacements[bucket] = 0;
					continue;
				}

				bool placed = false;
				for (uint32_t disp = 0; disp < 100000 && !placed; ++disp) {
					uint32_t claimed[N] = {};
					uint32_t claimedCount = 0;
					bool clash = false;
					for (size_t k = 0; k < N && !clash; ++k) {
						if (bucketOf(r_Keys[k], bucketCount) != bucket) continue;
						const uint32_t slot = slotOf(r_Keys[k], disp, slotCount);
						if (result.m_SlotToKey[slot] != kEmptySlot) clash = true;
						for (uint32_t c = 0; c < claimedCount && !clash; ++c)
							if (claimed[c] == slot) clash = true;
						if (!clash) claimed[claimedCount++] = slot;
					}
					if (clash) continue;

					uint32_t c = 0;
					for (size_t k = 0; k < N; ++k)
						if (bucketOf(r_Keys[k], bucketCount) == bucket) result.m_SlotToKey[claimed[c++]] = static_cast<uint32_t>(k);
					result.m_Displacements[bucket] = disp;
					placed = true;
				}
				ok = placed;
			}

			if (ok) {
				result.m_BucketCount = bucketCount;
				result.m_SlotCount = slotCount;
				result.m_Ok = true;
				return result;
			}
		}
		return result;
	}

}
