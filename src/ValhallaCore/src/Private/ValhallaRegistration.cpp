#include "ValhallaRegistration.h"
#include "ValhallaPrimitiveTypes.h"

namespace Valhalla {

	namespace {
		// Constant-initialized (nullptr), never itself an object with static-init
		// ordering concerns - only ever pointer-swapped, never read before Init().
		RegistrationNode* s_Head = nullptr;
		bool s_Initialized = false;

		void registerPrimitives(RegistrationHook p_Hook) {
			for (size_t i = 0; i < kPrimitiveTypeCount; ++i) {
				if (p_Hook) p_Hook(kPrimitiveTypes[i]);
			}
		}

		// ValhallaCore's own first registrant, proving the pattern works before
		// Valhalla-Gen exists to emit the equivalent per-module node. This
		// constructor runs at static-init time, in whatever order the linker
		// picks - fine, since all it does is push onto s_Head.
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
