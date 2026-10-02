#pragma once
#include <cstdint>

namespace Valhalla {

	enum class ObjectType : uint8_t {
		Class,
		Struct,
		Primitive,
		Enum
	};

	enum class Visibility : uint8_t {
		Public,
		Protected,
		Private
	};

	enum class LangTraits : uint32_t {
		None       = 0,
		Static     = 1u << 0,
		Const      = 1u << 1,
		Override   = 1u << 2,
		Overridden = 1u << 3,
		Abstract   = 1u << 4,
		Final      = 1u << 5
	};

	constexpr LangTraits operator|(LangTraits v_Lhs, LangTraits v_Rhs) noexcept {
		return static_cast<LangTraits>(static_cast<uint32_t>(v_Lhs) | static_cast<uint32_t>(v_Rhs));
	}
	constexpr LangTraits operator&(LangTraits v_Lhs, LangTraits v_Rhs) noexcept {
		return static_cast<LangTraits>(static_cast<uint32_t>(v_Lhs) & static_cast<uint32_t>(v_Rhs));
	}
	constexpr bool hasFlag(LangTraits v_Value, LangTraits v_Flag) noexcept {
		return (v_Value & v_Flag) == v_Flag;
	}

	enum class ReflectTraits : uint32_t {
		None      = 0,
		Pointer   = 1u << 0,
		Reference = 1u << 1,
		Functor   = 1u << 2
	};

	constexpr ReflectTraits operator|(ReflectTraits v_Lhs, ReflectTraits v_Rhs) noexcept {
		return static_cast<ReflectTraits>(static_cast<uint32_t>(v_Lhs) | static_cast<uint32_t>(v_Rhs));
	}
	constexpr ReflectTraits operator&(ReflectTraits v_Lhs, ReflectTraits v_Rhs) noexcept {
		return static_cast<ReflectTraits>(static_cast<uint32_t>(v_Lhs) & static_cast<uint32_t>(v_Rhs));
	}
	constexpr bool hasFlag(ReflectTraits v_Value, ReflectTraits v_Flag) noexcept {
		return (v_Value & v_Flag) == v_Flag;
	}

	enum class ToolTraits : uint32_t {
		None        = 0,
		Deprecated  = 1u << 0,
		Discardable = 1u << 1
	};

	constexpr ToolTraits operator|(ToolTraits v_Lhs, ToolTraits v_Rhs) noexcept {
		return static_cast<ToolTraits>(static_cast<uint32_t>(v_Lhs) | static_cast<uint32_t>(v_Rhs));
	}
	constexpr ToolTraits operator&(ToolTraits v_Lhs, ToolTraits v_Rhs) noexcept {
		return static_cast<ToolTraits>(static_cast<uint32_t>(v_Lhs) & static_cast<uint32_t>(v_Rhs));
	}
	constexpr bool hasFlag(ToolTraits v_Value, ToolTraits v_Flag) noexcept {
		return (v_Value & v_Flag) == v_Flag;
	}

	enum class OperatorKind : uint8_t {
		Add, Sub, Mul, Div, Mod,
		AddAssign, SubAssign, MulAssign, DivAssign, ModAssign,
		Equal, NotEqual, Less, LessEqual, Greater, GreaterEqual, Spaceship,
		LogicalAnd, LogicalOr, LogicalNot,
		BitAnd, BitOr, BitXor, BitNot, ShiftLeft, ShiftRight,
		BitAndAssign, BitOrAssign, BitXorAssign, ShiftLeftAssign, ShiftRightAssign,
		Assign, Subscript, Call, Arrow, Cast, Increment, Decrement
	};

	enum class ConstructorKind : uint8_t {
		Default,
		Copy,
		Move,
		Custom
	};

}
