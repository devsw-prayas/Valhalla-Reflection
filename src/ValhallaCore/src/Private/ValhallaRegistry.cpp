#include "ValhallaRegistry.h"
#include "ValhallaDiagnostic.h"

namespace Valhalla {

	namespace {
		constexpr uint32_t kModuleTableSize = VALHALLA_MAX_MODULES * 2;
		VALHALLA_STATIC_ASSERT((kModuleTableSize & (kModuleTableSize - 1)) == 0, "VALHALLA_MAX_MODULES must be a power of two");

		const ModuleRegistry* s_ModuleTable[kModuleTableSize] = {};
		const ModuleRegistry* s_ModuleOrder[VALHALLA_MAX_MODULES] = {};
		uint32_t s_ModuleCount = 0;

		uint32_t probeStart(uint64_t v_DllHash) {
			return static_cast<uint32_t>(Chd::mix(v_DllHash) & (kModuleTableSize - 1));
		}
	}

	void RegisterModule(const ModuleRegistry* p_Module, RegistrationHook p_Hook) {
		VALHALLA_ASSERT(p_Module != nullptr);
		VALHALLA_ASSERT(s_ModuleCount < VALHALLA_MAX_MODULES);
		if (s_ModuleCount >= VALHALLA_MAX_MODULES) return;

		uint32_t index = probeStart(p_Module->m_DllHash);
		while (s_ModuleTable[index] != nullptr) {
			VALHALLA_ASSERT(s_ModuleTable[index]->m_DllHash != p_Module->m_DllHash);
			if (s_ModuleTable[index]->m_DllHash == p_Module->m_DllHash) return;
			index = (index + 1) & (kModuleTableSize - 1);
		}
		s_ModuleTable[index] = p_Module;
		s_ModuleOrder[s_ModuleCount++] = p_Module;

		if (!p_Hook) return;
		for (uint32_t i = 0; i < p_Module->m_SlotCount; ++i) {
			if (p_Module->m_Slots[i] != nullptr) p_Hook(p_Module->m_Slots[i]);
		}
	}

	const ModuleRegistry* GetModule(uint64_t p_DllHash) {
		uint32_t index = probeStart(p_DllHash);
		while (s_ModuleTable[index] != nullptr) {
			if (s_ModuleTable[index]->m_DllHash == p_DllHash) return s_ModuleTable[index];
			index = (index + 1) & (kModuleTableSize - 1);
		}
		return nullptr;
	}

	const TypeDescriptor* GetType(uint64_t p_DllHash, uint64_t p_TypeHash) {
		const ModuleRegistry* module = GetModule(p_DllHash);
		return module ? lookupType(*module, p_TypeHash) : nullptr;
	}

	const TypeDescriptor* FindType(uint64_t p_TypeHash) {
		for (uint32_t i = 0; i < s_ModuleCount; ++i) {
			if (const TypeDescriptor* type = lookupType(*s_ModuleOrder[i], p_TypeHash)) return type;
		}
		return nullptr;
	}

	uint32_t GetModuleCount() {
		return s_ModuleCount;
	}

	const ModuleRegistry* GetModuleAt(uint32_t p_Index) {
		return p_Index < s_ModuleCount ? s_ModuleOrder[p_Index] : nullptr;
	}

}
