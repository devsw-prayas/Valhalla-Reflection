#pragma once
#include <cstdint>
#include "ValhallaTypes.h"
#include "ValhallaInvoke.h"

namespace Valhalla {

	struct TypeDescriptor;

	struct ParamDescriptor {
		const char* m_Name;
		const TypeDescriptor* m_Type;
		LangTraits m_LangTraits;
		ReflectTraits m_ReflectTraits;
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
		// Points at a stored member-function pointer, not the function itself: member pointers don't fit in a void*.
		const void* m_FuncPtr;
		ThunkFn m_Thunk;

		template <typename TReturn = void, typename... TArgs>
			requires(sizeof...(TArgs) <= VALHALLA_MAX_PARAMS)
		TReturn Invoke(void* p_Instance, TArgs&&... u_Args) const {
			VALHALLA_ASSERT(sizeof...(TArgs) == m_ParamCount);
			VALHALLA_ASSERT(p_Instance != nullptr || hasFlag(m_LangTraits, LangTraits::Static));
			VALHALLA_ASSERT(m_Thunk != nullptr);
			if constexpr (!std::is_void_v<TReturn>) VALHALLA_ASSERT(m_ReturnType != nullptr);
			return Detail::invokeThunk<TReturn>(m_Thunk, p_Instance, std::forward<TArgs>(u_Args)...);
		}

		template <typename TFn>
		TFn GetFunctor() const {
			VALHALLA_ASSERT(m_FuncPtr != nullptr);
			return *static_cast<const TFn*>(m_FuncPtr);
		}
	};

	struct OperatorDescriptor {
		OperatorKind m_Op;
		const char* m_Name;
		uint64_t m_Hash;
		Visibility m_Visibility;
		LangTraits m_LangTraits;
		const TypeDescriptor* m_ReturnType;
		const ParamDescriptor* m_Params;
		uint32_t m_ParamCount;
		ThunkFn m_Thunk;

		template <typename TReturn = void, typename... TArgs>
			requires(sizeof...(TArgs) <= VALHALLA_MAX_PARAMS)
		TReturn Invoke(void* p_Instance, TArgs&&... u_Args) const {
			VALHALLA_ASSERT(sizeof...(TArgs) == m_ParamCount);
			VALHALLA_ASSERT(p_Instance != nullptr || hasFlag(m_LangTraits, LangTraits::Static));
			VALHALLA_ASSERT(m_Thunk != nullptr);
			if constexpr (!std::is_void_v<TReturn>) VALHALLA_ASSERT(m_ReturnType != nullptr);
			return Detail::invokeThunk<TReturn>(m_Thunk, p_Instance, std::forward<TArgs>(u_Args)...);
		}
	};

	struct ConstructorDescriptor {
		Visibility m_Visibility;
		ConstructorKind m_Kind;
		const ParamDescriptor* m_Params;
		uint32_t m_ParamCount;
		ConstructorThunkFn m_Thunk;

		template <typename... TArgs>
			requires(sizeof...(TArgs) <= VALHALLA_MAX_PARAMS)
		void Construct(void* p_Memory, TArgs&&... u_Args) const {
			VALHALLA_ASSERT(sizeof...(TArgs) == m_ParamCount);
			VALHALLA_ASSERT(p_Memory != nullptr);
			Detail::PackedArgs<std::remove_reference_t<TArgs>...> packed(u_Args...);
			m_Thunk(p_Memory, packed.m_Slots);
		}
	};

	struct EnumValueDescriptor {
		const char* m_Name;
		const char* m_DisplayName;
		int64_t m_Value;
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
		const EnumValueDescriptor* m_EnumValues;
		uint32_t m_EnumValueCount;
		DestructorThunkFn m_Destructor;
	};

}
