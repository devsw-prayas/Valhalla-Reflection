#pragma once
#include <cstdint>
#include <memory>
#include <new>
#include <tuple>
#include <type_traits>
#include <utility>
#include "ValhallaDiagnostic.h"

#ifndef VALHALLA_MAX_PARAMS
#define VALHALLA_MAX_PARAMS 16
#endif

namespace Valhalla {

	using ThunkFn = void (*)(void* p_Instance, void** p_Args, void* p_Return);
	using ConstructorThunkFn = void (*)(void* p_Memory, void** p_Args);
	using DestructorThunkFn = void (*)(void* p_Instance);

}

namespace Valhalla::Detail {

	template <typename TFn>
	struct FunctionTraits;

	template <typename TRet, typename TClass, typename... TParams>
	struct FunctionTraits<TRet (TClass::*)(TParams...)> {
		using Class = TClass;
		using Return = TRet;
		static constexpr bool kIsMember = true;
		template <size_t I> using Param = std::tuple_element_t<I, std::tuple<TParams...>>;
		static constexpr size_t kArity = sizeof...(TParams);
	};

	template <typename TRet, typename TClass, typename... TParams>
	struct FunctionTraits<TRet (TClass::*)(TParams...) const> {
		using Class = const TClass;
		using Return = TRet;
		static constexpr bool kIsMember = true;
		template <size_t I> using Param = std::tuple_element_t<I, std::tuple<TParams...>>;
		static constexpr size_t kArity = sizeof...(TParams);
	};

	template <typename TRet, typename... TParams>
	struct FunctionTraits<TRet (*)(TParams...)> {
		using Class = void;
		using Return = TRet;
		static constexpr bool kIsMember = false;
		template <size_t I> using Param = std::tuple_element_t<I, std::tuple<TParams...>>;
		static constexpr size_t kArity = sizeof...(TParams);
	};

	// By-value params copy from the caller's object; only an rvalue-reference param may move from it.
	template <typename TParam>
	decltype(auto) argCast(void* p_Arg) {
		using Pointee = std::remove_reference_t<TParam>;
		if constexpr (std::is_rvalue_reference_v<TParam>) return static_cast<TParam>(*static_cast<Pointee*>(p_Arg));
		else return *static_cast<Pointee*>(p_Arg);
	}

	template <typename TRet, typename TCall>
	void storeReturn(void* p_Return, TCall&& u_Call) {
		if constexpr (std::is_void_v<TRet>) {
			u_Call();
		} else if constexpr (std::is_reference_v<TRet>) {
			auto&& result = u_Call();
			if (p_Return) *static_cast<std::remove_reference_t<TRet>**>(p_Return) = std::addressof(result);
		} else {
			if (p_Return) ::new (p_Return) std::remove_cv_t<TRet>(u_Call());
			else (void)u_Call();
		}
	}

	template <auto Fn, size_t... I>
	void callThunk(void* p_Instance, void** p_Args, void* p_Return, std::index_sequence<I...>) {
		using Traits = FunctionTraits<decltype(Fn)>;
		if constexpr (Traits::kIsMember) {
			auto* instance = static_cast<typename Traits::Class*>(p_Instance);
			storeReturn<typename Traits::Return>(p_Return, [&]() -> decltype(auto) {
				return (instance->*Fn)(argCast<typename Traits::template Param<I>>(p_Args[I])...);
			});
		} else {
			(void)p_Instance;
			storeReturn<typename Traits::Return>(p_Return, [&]() -> decltype(auto) {
				return Fn(argCast<typename Traits::template Param<I>>(p_Args[I])...);
			});
		}
		(void)p_Args;
	}

	template <auto Fn>
	void methodThunk(void* p_Instance, void** p_Args, void* p_Return) {
		callThunk<Fn>(p_Instance, p_Args, p_Return, std::make_index_sequence<FunctionTraits<decltype(Fn)>::kArity>{});
	}

	template <typename T>
	void defaultConstructThunk(void* p_Memory, void**) {
		::new (p_Memory) T();
	}

	template <typename T>
	void copyConstructThunk(void* p_Memory, void** p_Args) {
		::new (p_Memory) T(*static_cast<const T*>(p_Args[0]));
	}

	template <typename T>
	void moveConstructThunk(void* p_Memory, void** p_Args) {
		::new (p_Memory) T(std::move(*static_cast<T*>(p_Args[0])));
	}

	template <typename T, typename... TParams, size_t... I>
	void customConstruct(void* p_Memory, void** p_Args, std::index_sequence<I...>) {
		::new (p_Memory) T(argCast<TParams>(p_Args[I])...);
		(void)p_Args;
	}

	template <typename T, typename... TParams>
	void customConstructThunk(void* p_Memory, void** p_Args) {
		customConstruct<T, TParams...>(p_Memory, p_Args, std::index_sequence_for<TParams...>{});
	}

	template <typename T>
	void destructThunk(void* p_Instance) {
		std::destroy_at(static_cast<T*>(p_Instance));
	}

	// Arguments must be the exact parameter types: the thunk reinterprets each pointer, there is no conversion.
	template <typename... TArgs>
	struct PackedArgs {
		void* m_Slots[sizeof...(TArgs) > 0 ? sizeof...(TArgs) : 1];

		explicit PackedArgs(TArgs&... r_Args) : m_Slots{ const_cast<void*>(static_cast<const volatile void*>(std::addressof(r_Args)))... } {}
	};

	template <typename TReturn, typename... TArgs>
	TReturn invokeThunk(ThunkFn v_Thunk, void* p_Instance, TArgs&&... u_Args) {
		PackedArgs<std::remove_reference_t<TArgs>...> packed(u_Args...);

		if constexpr (std::is_void_v<TReturn>) {
			v_Thunk(p_Instance, packed.m_Slots, nullptr);
		} else if constexpr (std::is_reference_v<TReturn>) {
			std::remove_reference_t<TReturn>* result = nullptr;
			v_Thunk(p_Instance, packed.m_Slots, &result);
			return static_cast<TReturn>(*result);
		} else {
			alignas(TReturn) unsigned char storage[sizeof(TReturn)];
			v_Thunk(p_Instance, packed.m_Slots, storage);
			TReturn* produced = std::launder(reinterpret_cast<TReturn*>(storage));
			TReturn result(std::move(*produced));
			std::destroy_at(produced);
			return result;
		}
	}

}
