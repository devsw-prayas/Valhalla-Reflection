#pragma once
#include <cstdint>
#include "ValhallaTypes.h"

namespace Valhalla {

	struct TypeDescriptor;

	using ThunkFn = void (*)(void* p_Instance, void** p_Args, void* p_Return);
	using ConstructorThunkFn = void (*)(void* p_Memory, void** p_Args);
	using DestructorThunkFn = void (*)(void* p_Instance);

	struct ParamDescriptor {
		const char* m_Name;
		const TypeDescriptor* m_Type;
		LangTraits m_LangTraits;
		uint32_t m_Index;
	};

	struct FieldDescriptor {
		const char* m_Name;
		const char* m_DisplayName;
		const char* m_Category;
		const char* m_Tooltip;
		uint32_t m_Offset;
		uint32_t m_Size;
		uint32_t m_Align;
		Visibility m_Visibility;
		LangTraits m_LangTraits;
		ReflectTraits m_ReflectTraits;
		ToolTraits m_ToolTraits;
		const TypeDescriptor* m_FieldType;
	};

	struct MethodDescriptor {
		const char* m_Name;
		const char* m_DisplayName;
		const char* m_Category;
		const char* m_Tooltip;
		uint64_t m_Hash;
		Visibility m_Visibility;
		LangTraits m_LangTraits;
		ReflectTraits m_ReflectTraits;
		ToolTraits m_ToolTraits;
		const TypeDescriptor* m_ReturnType;
		const ParamDescriptor* m_Params;
		uint32_t m_ParamCount;
		void* m_FuncPtr;
		ThunkFn m_Thunk;
	};

	struct OperatorDescriptor {
		OperatorKind m_Op;
		uint64_t m_Hash;
		Visibility m_Visibility;
		LangTraits m_LangTraits;
		const TypeDescriptor* m_ReturnType;
		const ParamDescriptor* m_Params;
		uint32_t m_ParamCount;
		ThunkFn m_Thunk;
	};

	struct ConstructorDescriptor {
		Visibility m_Visibility;
		ConstructorKind m_Kind;
		ConstructorThunkFn m_Thunk;
	};

	struct TypeDescriptor {
		const char* m_Name;
		const char* m_DisplayName;
		const char* m_Category;
		const char* m_Tooltip;
		uint64_t m_Hash;
		ObjectType m_ObjectType;
		uint32_t m_Size;
		uint32_t m_Align;
		const TypeDescriptor* m_Parent;
		const FieldDescriptor* m_Fields;
		uint32_t m_FieldCount;
		const MethodDescriptor* m_Methods;
		uint32_t m_MethodCount;
		const OperatorDescriptor* m_Operators;
		uint32_t m_OperatorCount;
		const ConstructorDescriptor* m_Constructors;
		uint32_t m_ConstructorCount;
		DestructorThunkFn m_Destructor;
	};

}
