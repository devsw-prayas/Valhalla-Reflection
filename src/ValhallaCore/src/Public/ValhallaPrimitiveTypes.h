#pragma once
#include <cstddef>
#include "ValhallaTypes.h"
#include "ValhallaDescriptors.h"
#include "ValhallaHash.h"
#include "ValhallaRegistry.h"

namespace Valhalla {

	// Inline constexpr: every DLL gets its own copy, so compare primitive descriptors by m_Hash, never by address.
#define VALHALLA_PRIMITIVE(name) \
	inline constexpr TypeDescriptor kType_##name = { \
		.m_Name = #name, \
		.m_DisplayName = #name, \
		.m_Category = "Primitive", \
		.m_Tooltip = "", \
		.m_Hash = fnv1a(#name), \
		.m_ObjectType = ObjectType::Primitive, \
		.m_Size = sizeof(name), \
		.m_Align = alignof(name), \
		.m_Parent = nullptr, \
		.m_Fields = nullptr, \
		.m_FieldCount = 0, \
		.m_Methods = nullptr, \
		.m_MethodCount = 0, \
		.m_Operators = nullptr, \
		.m_OperatorCount = 0, \
		.m_Constructors = nullptr, \
		.m_ConstructorCount = 0, \
		.m_EnumValues = nullptr, \
		.m_EnumValueCount = 0, \
		.m_Destructor = nullptr \
	};
#include "ValhallaPrimitives.def"

	inline constexpr const TypeDescriptor* kPrimitiveTypes[] = {
#define VALHALLA_PRIMITIVE(name) &kType_##name,
#include "ValhallaPrimitives.def"
	};

	inline constexpr size_t kPrimitiveTypeCount = sizeof(kPrimitiveTypes) / sizeof(kPrimitiveTypes[0]);

	inline constexpr uint64_t kPrimitivesModuleHash = fnv1a("Valhalla.Primitives");

}
