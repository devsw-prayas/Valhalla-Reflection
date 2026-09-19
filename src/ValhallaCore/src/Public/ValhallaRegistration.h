#pragma once
#include "ValhallaCore.h"
#include "ValhallaDescriptors.h"

namespace Valhalla {

	using RegistrationHook = void (*)(const TypeDescriptor*);
	using RegisterAllFn = void (*)(RegistrationHook p_Hook);

	struct RegistrationNode {
		RegistrationNode* m_Next;
		RegisterAllFn m_RegisterAll;
	};

	ValhallaCore_API void pushRegistration(RegistrationNode* p_Node);

	ValhallaCore_API void Init(RegistrationHook p_Hook = nullptr);

}
