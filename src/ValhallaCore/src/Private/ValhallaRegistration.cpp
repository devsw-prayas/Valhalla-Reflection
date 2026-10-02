#include "ValhallaRegistration.h"
#include "ValhallaPrimitiveTypes.h"

namespace Valhalla {

	namespace {
		RegistrationNode* s_Head = nullptr;
		bool s_Initialized = false;

		constexpr uint64_t kPrimitiveKeys[] = {
#define VALHALLA_PRIMITIVE(name) fnv1a(#name),
#include "ValhallaPrimitives.def"
		};

		constexpr auto kPrimitiveChd = Chd::build(kPrimitiveKeys);
		VALHALLA_STATIC_ASSERT(kPrimitiveChd.m_Ok, "Primitive CHD table failed to build");

		constexpr auto kPrimitiveSlots = [] {
			struct Slots { const TypeDescriptor* m_Types[kPrimitiveTypeCount * 2]; } slots{};
			for (uint32_t s = 0; s < kPrimitiveChd.m_SlotCount; ++s) {
				const uint32_t key = kPrimitiveChd.m_SlotToKey[s];
				slots.m_Types[s] = key == Chd::kEmptySlot ? nullptr : kPrimitiveTypes[key];
			}
			return slots;
		}();

		constexpr ModuleRegistry kPrimitiveModule = {
			.m_Name = "Valhalla.Primitives",
			.m_DllHash = kPrimitivesModuleHash,
			.m_BucketCount = kPrimitiveChd.m_BucketCount,
			.m_SlotCount = kPrimitiveChd.m_SlotCount,
			.m_Displacements = kPrimitiveChd.m_Displacements,
			.m_Slots = kPrimitiveSlots.m_Types,
			.m_TypeCount = static_cast<uint32_t>(kPrimitiveTypeCount),
		};

		void registerPrimitives(RegistrationHook p_Hook) {
			RegisterModule(&kPrimitiveModule, p_Hook);
		}

		const AutoRegistration s_PrimitiveRegistration(&registerPrimitives);
	}

	void pushRegistration(RegistrationNode* p_Node) {
		p_Node->m_Next = s_Head;
		s_Head = p_Node;
	}

	void Init(RegistrationHook p_Hook) {
		if (s_Initialized) return;
		s_Initialized = true;

		for (RegistrationNode* node = s_Head; node != nullptr; node = node->m_Next) {
			node->m_RegisterAll(p_Hook);
		}
	}

	bool IsInitialized() {
		return s_Initialized;
	}

}
