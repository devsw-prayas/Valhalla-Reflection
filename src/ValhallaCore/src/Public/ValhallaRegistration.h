#pragma once
#include "ValhallaCore.h"
#include "ValhallaRegistry.h"

namespace Valhalla {

	using RegisterAllFn = void (*)(RegistrationHook p_Hook);

	struct RegistrationNode {
		RegistrationNode* m_Next;
		RegisterAllFn m_RegisterAll;
	};

	ValhallaCore_API void pushRegistration(RegistrationNode* p_Node);

	ValhallaCore_API void Init(RegistrationHook p_Hook = nullptr);
	ValhallaCore_API bool IsInitialized();

	// One per generated module: the only work it does at static-init time is the list prepend.
	struct AutoRegistration {
		RegistrationNode m_Node;

		explicit AutoRegistration(RegisterAllFn p_RegisterAll) : m_Node{ nullptr, p_RegisterAll } {
			pushRegistration(&m_Node);
		}
	};

}
