#pragma once
#include <cstdint>
#include "ValhallaMacros.h"

namespace Smoke {

	VALHALLA_ENUM(DisplayName="Light Type", Category="Rendering/Lighting")
	enum class LightType : uint8_t {
		VALHALLA_ENUM_VAL(DisplayName="Point Light", Value=10)
		Point,
		VALHALLA_ENUM_VAL(DisplayName="Directional")
		Directional,
		Spot
	};

	VALHALLA_TYPE(define=ObjectType.Struct, DisplayName="Vector 3")
	struct Vec3 {
		VALHALLA_FIELD()
		float x = 0.0f;
		VALHALLA_FIELD()
		float y = 0.0f;
		VALHALLA_FIELD()
		float z = 0.0f;

		VALHALLA_CONSTRUCTOR(Default=Constructor.Default)
		VALHALLA_CONSTRUCTOR(Default=Constructor.Copy)
		VALHALLA_CONSTRUCTOR()
		Vec3(float v_X, float v_Y, float v_Z) : x(v_X), y(v_Y), z(v_Z) {}
		Vec3() = default;

		VALHALLA_METHOD(LangTraits={Behave.Const}, ReflectTraits={Behave.Functor})
		float dot(const Vec3& r_Other) const { return x * r_Other.x + y * r_Other.y + z * r_Other.z; }

		VALHALLA_OPERATOR(Op=Operator.Plus, LangTraits={Behave.Const})
		Vec3 operator+(const Vec3& r_Other) const { return { x + r_Other.x, y + r_Other.y, z + r_Other.z }; }

		VALHALLA_OPERATOR(Op=Operator.Equals, LangTraits={Behave.Const})
		bool operator==(const Vec3& r_Other) const { return x == r_Other.x && y == r_Other.y && z == r_Other.z; }

		VALHALLA_OPERATOR(Op=Operator.Subscript)
		float& operator[](uint32_t v_Index) { return v_Index == 0 ? x : (v_Index == 1 ? y : z); }
	};

	VALHALLA_TYPE(define=ObjectType.Class, DisplayName="Scene Object", Category="Scene")
	struct SceneObject {
		virtual ~SceneObject() = default;

		VALHALLA_FIELD(Visibility=Public)
		uint32_t m_Id = 0;

		VALHALLA_METHOD(Visibility=Public, LangTraits={Behave.Abstract, Behave.Override, Behave.Const})
		virtual float intensity() const = 0;

		VALHALLA_METHOD(Visibility=Public, LangTraits={Behave.Override})
		virtual void setIntensity(float v_Value) { (void)v_Value; }
	};

	VALHALLA_TYPE(define=ObjectType.Class, Parent=SceneObject, DisplayName="Light", Category="Rendering/Lighting", Tooltip="A \"quoted\" tooltip")
	VALHALLA_LAYOUT(Align=16)
	struct alignas(16) Light : public SceneObject {
		VALHALLA_FIELD(Visibility=Public)
		Vec3 m_Color;
		VALHALLA_FIELD()
		float m_Intensity = 1.0f;
		VALHALLA_FIELD(Visibility=Public)
		LightType m_Type = LightType::Point;
		VALHALLA_FIELD(Visibility=Public)
		Light* m_Next = nullptr;
		VALHALLA_FIELD(LangTraits={Behave.Static}, ToolTraits={Behave.Deprecated})
		static inline int s_LiveCount = 7;

		VALHALLA_CONSTRUCTOR(Visibility=Public, Default=Constructor.Default)
		VALHALLA_CONSTRUCTOR(Visibility=Public, Default=Constructor.Move)

		VALHALLA_METHOD(Visibility=Public, LangTraits={Behave.Overridden, Behave.Const})
		float intensity() const override { return m_Intensity; }

		VALHALLA_METHOD(Visibility=Public, LangTraits={Behave.Overridden})
		void setIntensity(float v_Value) override { m_Intensity = v_Value; }

		VALHALLA_METHOD(Visibility=Public)
		void setColor(const Vec3& r_Color, float v_Scale) { m_Color = Vec3{ r_Color.x * v_Scale, r_Color.y * v_Scale, r_Color.z * v_Scale }; }

		VALHALLA_METHOD(Visibility=Public)
		void setColor(float v_Grey) { m_Color = Vec3{ v_Grey, v_Grey, v_Grey }; }

		VALHALLA_METHOD(Visibility=Public, LangTraits={Behave.Static})
		static int liveCount() { return s_LiveCount; }

		VALHALLA_METHOD(Visibility=Public)
		Vec3& color() { return m_Color; }

		VALHALLA_OPERATOR(Op=Operator.Cast, Visibility=Public, LangTraits={Behave.Const})
		explicit operator float() const { return m_Intensity; }
	};

	VALHALLA_TYPE(define=ObjectType.Struct, DisplayName="Array")
	template <typename T>
	struct RArray {
		VALHALLA_FIELD()
		T* m_Data = nullptr;
		VALHALLA_FIELD()
		uint32_t m_Count = 0;

		VALHALLA_METHOD(LangTraits={Behave.Const})
		uint32_t size() const { return m_Count; }

		VALHALLA_METHOD(LangTraits={Behave.Const})
		const T& at(uint32_t v_Index) const { return m_Data[v_Index]; }
	};

	VALHALLA_SPECIALIZE(RArray<float>)
	VALHALLA_SPECIALIZE(RArray<Vec3>)

}
