#include "ValhallaRegistration.h"
#include "ValhallaPrimitiveTypes.h"
#include "ValhallaRegistry.h"

namespace Valhalla {

	namespace {
		RegistrationNode* s_Head = nullptr;
		bool s_Initialized = false;

		void registerPrimitives(RegistrationHook p_Hook) {
			for (size_t i = 0; i < kPrimitiveTypeCount; ++i) {
				RegisterType(kPrimitivesModuleHash, kPrimitiveTypes[i]);
				if (p_Hook) p_Hook(kPrimitiveTypes[i]);
			}
		}

		struct PrimitiveRegistration {
			RegistrationNode m_Node{ nullptr, &registerPrimitives };
			PrimitiveRegistration() { pushRegistration(&m_Node); }
		};
		const PrimitiveRegistration s_PrimitiveRegistration;
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

}
