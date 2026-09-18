#pragma once
#include "ValhallaCore.h"
#include "ValhallaDescriptors.h"

namespace Valhalla {

	// Fired once per registered type during Init(), if a hook was supplied.
	using RegistrationHook = void (*)(const TypeDescriptor*);

	// A module's own registration entry point - iterates whatever types that
	// module owns, invoking p_Hook for each (and, from Commit 4 onward, also
	// inserting each into the runtime registry).
	using RegisterAllFn = void (*)(RegistrationHook p_Hook);

	// Intrusive singly-linked list node. One of these lives as a static object
	// per module (see ValhallaRegistration.cpp for ValhallaCore's own primitive-
	// type node); its constructor prepends itself onto s_Head via
	// pushRegistration(). Per addendum v1.1 §A.2, this is deliberately just a
	// pointer-swap onto a constant-initialized head - safe regardless of
	// static-init order across translation units, unlike v1.0 §4.3's original
	// "static ctor calls RegisterType() directly" design, which this supersedes.
	struct RegistrationNode {
		RegistrationNode* m_Next;
		RegisterAllFn m_RegisterAll;
	};

	ValhallaCore_API void pushRegistration(RegistrationNode* p_Node);

	// Walks every pushed RegistrationNode and calls its RegisterAll() once.
	// Second and later calls are a permanent no-op in every build config -
	// this is a real invariant of the system, not a debug-only guard.
	ValhallaCore_API void Init(RegistrationHook p_Hook = nullptr);

}
