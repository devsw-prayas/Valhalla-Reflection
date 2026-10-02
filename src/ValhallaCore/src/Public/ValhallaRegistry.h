#pragma once
#include "ValhallaCore.h"
#include "ValhallaChd.h"
#include "ValhallaDescriptors.h"

#ifndef VALHALLA_MAX_MODULES
#define VALHALLA_MAX_MODULES 64
#endif

namespace Valhalla {

	struct ModuleRegistry {
		const char* m_Name;
		uint64_t m_DllHash;
		uint32_t m_BucketCount;
		uint32_t m_SlotCount;
		const uint32_t* m_Displacements;
		const TypeDescriptor* const* m_Slots;
		uint32_t m_TypeCount;
	};

	// The slot hash proves nothing about membership, so the stored hash is checked to reject unknown keys.
	constexpr const TypeDescriptor* lookupType(const ModuleRegistry& r_Module, uint64_t v_TypeHash) noexcept {
		if (r_Module.m_SlotCount == 0) return nullptr;
		const uint32_t bucket = Chd::bucketOf(v_TypeHash, r_Module.m_BucketCount);
		const uint32_t slot = Chd::slotOf(v_TypeHash, r_Module.m_Displacements[bucket], r_Module.m_SlotCount);
		const TypeDescriptor* type = r_Module.m_Slots[slot];
		return (type != nullptr && type->m_Hash == v_TypeHash) ? type : nullptr;
	}

	using RegistrationHook = void (*)(const TypeDescriptor*);

	ValhallaCore_API void RegisterModule(const ModuleRegistry* p_Module, RegistrationHook p_Hook);

	ValhallaCore_API const ModuleRegistry* GetModule(uint64_t p_DllHash);
	ValhallaCore_API const TypeDescriptor* GetType(uint64_t p_DllHash, uint64_t p_TypeHash);
	// Walks every registered module; use GetType when the owning module is known.
	ValhallaCore_API const TypeDescriptor* FindType(uint64_t p_TypeHash);
	ValhallaCore_API uint32_t GetModuleCount();
	ValhallaCore_API const ModuleRegistry* GetModuleAt(uint32_t p_Index);

}
