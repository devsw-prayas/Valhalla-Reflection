#include <cstddef>
#include <cstdio>
#include <cstring>
#include <new>
#include "Valhalla.h"
#include "SmokeTypes.h"
#include "Light.generated.h"
#include "LightType.generated.h"
#include "RArray.generated.h"
#include "SceneObject.generated.h"
#include "Vec3.generated.h"
#include "ValhallaSmokeRegistry.generated.h"

#if defined(__clang__)
#pragma clang diagnostic ignored "-Winvalid-offsetof"
#endif

namespace {

	int g_Failures = 0;
	int g_Checks = 0;
	int g_Hooked = 0;

#define SMOKE_CHECK(expr) \
	do { \
		++g_Checks; \
		if (!(expr)) { \
			std::printf("FAIL %s:%d: %s\n", __FILE__, __LINE__, #expr); \
			++g_Failures; \
		} \
	} while (0)

	void countHook(const Valhalla::TypeDescriptor*) {
		++g_Hooked;
	}

	template <typename TDesc>
	const TDesc* findByName(const TDesc* p_Items, uint32_t v_Count, const char* p_Name, uint32_t v_ParamCount) {
		for (uint32_t i = 0; i < v_Count; ++i)
			if (std::strcmp(p_Items[i].m_Name, p_Name) == 0 && p_Items[i].m_ParamCount == v_ParamCount) return &p_Items[i];
		return nullptr;
	}

	const Valhalla::FieldDescriptor* findField(const Valhalla::TypeDescriptor* p_Type, const char* p_Name) {
		for (uint32_t i = 0; i < p_Type->m_FieldCount; ++i)
			if (std::strcmp(p_Type->m_Fields[i].m_Name, p_Name) == 0) return &p_Type->m_Fields[i];
		return nullptr;
	}

	const Valhalla::OperatorDescriptor* findOperator(const Valhalla::TypeDescriptor* p_Type, Valhalla::OperatorKind v_Op) {
		for (uint32_t i = 0; i < p_Type->m_OperatorCount; ++i)
			if (p_Type->m_Operators[i].m_Op == v_Op) return &p_Type->m_Operators[i];
		return nullptr;
	}

	using LightCtls = Valhalla::CTLS::Smoke::Light;
	using LightTypeCtls = Valhalla::CTLS::Smoke::LightType;

	static_assert(LightCtls::kAlign == 16);
	static_assert(LightCtls::kObjectType == Valhalla::ObjectType::Class);
	static_assert(LightCtls::kParentHash == Valhalla::CTLS::Smoke::SceneObject::kTypeHash);
	static_assert(LightCtls::Fields::m_Intensity::kOffset == offsetof(Smoke::Light, m_Intensity));
	static_assert(LightCtls::Fields::m_Intensity::kVisibility == Valhalla::Visibility::Private);
	static_assert(LightCtls::Fields::s_LiveCount::kOffset == 0);
	static_assert(LightCtls::kTypeHash == Valhalla::fnv1a("Smoke::Light"));
	static_assert(Valhalla::CTLS::Smoke::Vec3::kObjectType == Valhalla::ObjectType::Struct);
	static_assert(Valhalla::CTLS::Smoke::RArray<float>::Fields::m_Data::kReflectTraits == Valhalla::ReflectTraits::Pointer);
	static_assert(LightTypeCtls::kCount == 3);
	static_assert(LightTypeCtls::ToInt(Smoke::LightType::Point) == 10);
	static_assert(LightTypeCtls::ToInt(Smoke::LightType::Spot) == static_cast<int64_t>(Smoke::LightType::Spot));

	void testRegistry() {
		using namespace Smoke::ValhallaTypeHashes;

		SMOKE_CHECK(!Valhalla::IsInitialized());
		SMOKE_CHECK(Smoke::GetReflectedType(kSmoke_Light) == nullptr);

		Valhalla::Init(&countHook);
		const int hookedOnce = g_Hooked;
		Valhalla::Init(&countHook);

		SMOKE_CHECK(Valhalla::IsInitialized());
		SMOKE_CHECK(g_Hooked == hookedOnce);
		SMOKE_CHECK(hookedOnce == static_cast<int>(Valhalla::kPrimitiveTypeCount) + 6);
		SMOKE_CHECK(Valhalla::GetModuleCount() == 2);

		const auto* f = Valhalla::GetType(Valhalla::kPrimitivesModuleHash, Valhalla::fnv1a("float"));
		SMOKE_CHECK(f != nullptr && f->m_Size == 4 && f->m_ObjectType == Valhalla::ObjectType::Primitive);
		SMOKE_CHECK(Valhalla::FindType(Valhalla::fnv1a("uint64_t")) != nullptr);
		SMOKE_CHECK(Valhalla::FindType(Valhalla::fnv1a("NotAType")) == nullptr);
		SMOKE_CHECK(Valhalla::GetType(Valhalla::fnv1a("NoSuchModule"), kSmoke_Light) == nullptr);

		for (uint64_t hash : { kSmoke_Vec3, kSmoke_SceneObject, kSmoke_Light, kSmoke_RArray_float, kSmoke_RArray_Smoke_Vec3, kSmoke_LightType }) {
			const auto* type = Smoke::GetReflectedType(hash);
			SMOKE_CHECK(type != nullptr && type->m_Hash == hash);
		}
	}

	void testDescriptors() {
		using namespace Smoke::ValhallaTypeHashes;
		const auto* light = Smoke::GetReflectedType(kSmoke_Light);
		const auto* scene = Smoke::GetReflectedType(kSmoke_SceneObject);
		const auto* vec3 = Smoke::GetReflectedType(kSmoke_Vec3);
		const auto* lightType = Smoke::GetReflectedType(kSmoke_LightType);

		SMOKE_CHECK(std::strcmp(light->m_Name, "Smoke::Light") == 0);
		SMOKE_CHECK(std::strcmp(light->m_Tooltip, "A \"quoted\" tooltip") == 0);
		SMOKE_CHECK(light->m_Parent == scene);
		SMOKE_CHECK(light->m_Size == sizeof(Smoke::Light) && light->m_Align == 16);
		SMOKE_CHECK(light->m_FieldCount == 5 && light->m_MethodCount == 6 && light->m_OperatorCount == 1 && light->m_ConstructorCount == 2);

		const auto* color = findField(light, "m_Color");
		SMOKE_CHECK(color && color->m_Offset == offsetof(Smoke::Light, m_Color) && color->m_FieldType == vec3);
		const auto* type = findField(light, "m_Type");
		SMOKE_CHECK(type && type->m_FieldType == lightType && type->m_Size == 1);
		const auto* next = findField(light, "m_Next");
		SMOKE_CHECK(next && next->m_FieldType == light && Valhalla::hasFlag(next->m_ReflectTraits, Valhalla::ReflectTraits::Pointer));
		const auto* live = findField(light, "s_LiveCount");
		SMOKE_CHECK(live && Valhalla::hasFlag(live->m_LangTraits, Valhalla::LangTraits::Static) && live->m_Offset == 0);
		SMOKE_CHECK(live && live->m_FieldType->m_Hash == Valhalla::fnv1a("int32_t"));
		SMOKE_CHECK(live && Valhalla::hasFlag(live->m_ToolTraits, Valhalla::ToolTraits::Deprecated));

		const auto* abstractIntensity = findByName(scene->m_Methods, scene->m_MethodCount, "intensity", 0);
		SMOKE_CHECK(abstractIntensity && abstractIntensity->m_Thunk == nullptr);

		SMOKE_CHECK(lightType->m_ObjectType == Valhalla::ObjectType::Enum && lightType->m_EnumValueCount == 3);
		SMOKE_CHECK(lightType->m_EnumValues[0].m_Value == 10 && std::strcmp(lightType->m_EnumValues[0].m_DisplayName, "Point Light") == 0);
		SMOKE_CHECK(std::strcmp(lightType->m_EnumValues[2].m_DisplayName, "Spot") == 0);

		const auto* arrayVec = Smoke::GetReflectedType(kSmoke_RArray_Smoke_Vec3);
		const auto* data = findField(arrayVec, "m_Data");
		SMOKE_CHECK(data && data->m_FieldType == vec3);
		SMOKE_CHECK(std::strcmp(arrayVec->m_Name, "Smoke::RArray<Smoke::Vec3>") == 0);
	}

	void testInvoke() {
		using namespace Smoke::ValhallaTypeHashes;
		const auto* light = Smoke::GetReflectedType(kSmoke_Light);
		Smoke::Light instance;

		const auto* setIntensity = findByName(light->m_Methods, light->m_MethodCount, "setIntensity", 1);
		const auto* intensity = findByName(light->m_Methods, light->m_MethodCount, "intensity", 0);
		float value = 3.5f;
		setIntensity->Invoke(&instance, value);
		SMOKE_CHECK(intensity->Invoke<float>(&instance) == 3.5f);

		const auto* setGrey = findByName(light->m_Methods, light->m_MethodCount, "setColor", 1);
		const auto* setScaled = findByName(light->m_Methods, light->m_MethodCount, "setColor", 2);
		setGrey->Invoke(&instance, 0.5f);
		SMOKE_CHECK(instance.m_Color == (Smoke::Vec3{ 0.5f, 0.5f, 0.5f }));
		setScaled->Invoke(&instance, Smoke::Vec3{ 1.0f, 2.0f, 3.0f }, 2.0f);
		SMOKE_CHECK(instance.m_Color == (Smoke::Vec3{ 2.0f, 4.0f, 6.0f }));

		const auto* liveCount = findByName(light->m_Methods, light->m_MethodCount, "liveCount", 0);
		SMOKE_CHECK(liveCount->Invoke<int>(nullptr) == 7);

		const auto* colorRef = findByName(light->m_Methods, light->m_MethodCount, "color", 0);
		SMOKE_CHECK(&colorRef->Invoke<Smoke::Vec3&>(&instance) == &instance.m_Color);

		const auto* cast = findOperator(light, Valhalla::OperatorKind::Cast);
		SMOKE_CHECK(cast && cast->Invoke<float>(&instance) == 3.5f);

		// Virtual dispatch through the base descriptor's thunk.
		const auto* scene = Smoke::GetReflectedType(kSmoke_SceneObject);
		const auto* baseSet = findByName(scene->m_Methods, scene->m_MethodCount, "setIntensity", 1);
		baseSet->Invoke(static_cast<Smoke::SceneObject*>(&instance), 9.0f);
		SMOKE_CHECK(instance.intensity() == 9.0f);

		const auto* vec3 = Smoke::GetReflectedType(kSmoke_Vec3);
		Smoke::Vec3 a{ 1.0f, 2.0f, 3.0f };
		Smoke::Vec3 b{ 4.0f, 5.0f, 6.0f };
		SMOKE_CHECK(findOperator(vec3, Valhalla::OperatorKind::Add)->Invoke<Smoke::Vec3>(&a, b) == (Smoke::Vec3{ 5.0f, 7.0f, 9.0f }));
		SMOKE_CHECK(findOperator(vec3, Valhalla::OperatorKind::Equal)->Invoke<bool>(&a, a));
		SMOKE_CHECK(!findOperator(vec3, Valhalla::OperatorKind::Equal)->Invoke<bool>(&a, b));
		findOperator(vec3, Valhalla::OperatorKind::Subscript)->Invoke<float&>(&a, uint32_t{ 1 }) = 42.0f;
		SMOKE_CHECK(a.y == 42.0f);

		const auto* dot = findByName(vec3->m_Methods, vec3->m_MethodCount, "dot", 1);
		using DotFn = float (Smoke::Vec3::*)(const Smoke::Vec3&) const;
		SMOKE_CHECK(dot->m_FuncPtr != nullptr && (b.*dot->GetFunctor<DotFn>())(b) == 77.0f);
		SMOKE_CHECK(dot->Invoke<float>(&b, b) == 77.0f);
	}

	void testConstruction() {
		using namespace Smoke::ValhallaTypeHashes;
		const auto* vec3 = Smoke::GetReflectedType(kSmoke_Vec3);
		alignas(Smoke::Vec3) unsigned char storage[sizeof(Smoke::Vec3)];

		const Valhalla::ConstructorDescriptor* custom = nullptr;
		const Valhalla::ConstructorDescriptor* copy = nullptr;
		for (uint32_t i = 0; i < vec3->m_ConstructorCount; ++i) {
			if (vec3->m_Constructors[i].m_Kind == Valhalla::ConstructorKind::Custom) custom = &vec3->m_Constructors[i];
			if (vec3->m_Constructors[i].m_Kind == Valhalla::ConstructorKind::Copy) copy = &vec3->m_Constructors[i];
		}
		custom->Construct(storage, 1.0f, 2.0f, 3.0f);
		auto* built = std::launder(reinterpret_cast<Smoke::Vec3*>(storage));
		SMOKE_CHECK(*built == (Smoke::Vec3{ 1.0f, 2.0f, 3.0f }));
		vec3->m_Destructor(built);

		const Smoke::Vec3 source{ 7.0f, 8.0f, 9.0f };
		copy->Construct(storage, source);
		built = std::launder(reinterpret_cast<Smoke::Vec3*>(storage));
		SMOKE_CHECK(*built == source);
		vec3->m_Destructor(built);

		const auto* light = Smoke::GetReflectedType(kSmoke_Light);
		alignas(Smoke::Light) unsigned char lightStorage[sizeof(Smoke::Light)];
		light->m_Constructors[0].Construct(lightStorage);
		auto* lightBuilt = std::launder(reinterpret_cast<Smoke::Light*>(lightStorage));
		SMOKE_CHECK(lightBuilt->intensity() == 1.0f);
		light->m_Destructor(lightBuilt);
	}

	void testCtls() {
		Smoke::Light instance;
		LightCtls::Methods::setIntensity::Invoke(&instance, 2.0f);
		SMOKE_CHECK(LightCtls::Methods::intensity::Invoke(&instance) == 2.0f);
		SMOKE_CHECK(LightCtls::Methods::liveCount::Invoke() == 7);

		float values[] = { 1.0f, 2.0f };
		Smoke::RArray<float> array{ values, 2 };
		SMOKE_CHECK(Valhalla::CTLS::Smoke::RArray<float>::Methods::size::Invoke(&array) == 2);
		SMOKE_CHECK(Valhalla::CTLS::Smoke::RArray<float>::Methods::at::Invoke(&array, 1u) == 2.0f);

		alignas(Smoke::Vec3) unsigned char storage[sizeof(Smoke::Vec3)];
		Smoke::Vec3* v = Valhalla::CTLS::Smoke::Vec3::Constructors::Custom::Invoke(storage, 1.0f, 1.0f, 1.0f);
		SMOKE_CHECK(v->x == 1.0f);
		Valhalla::CTLS::Smoke::Vec3::Destructor::Invoke(v);

		SMOKE_CHECK(std::strcmp(LightTypeCtls::ToString(Smoke::LightType::Spot), "Spot") == 0);
		SMOKE_CHECK(std::strcmp(LightTypeCtls::ToDisplayName(Smoke::LightType::Point), "Point Light") == 0);
		Smoke::LightType parsed{};
		SMOKE_CHECK(LightTypeCtls::FromString("Directional", parsed) && parsed == Smoke::LightType::Directional);
		SMOKE_CHECK(LightTypeCtls::FromInt(10, parsed) && parsed == Smoke::LightType::Point);
		SMOKE_CHECK(!LightTypeCtls::FromString("Nope", parsed));
	}

}

int main() {
	testRegistry();
	testDescriptors();
	testInvoke();
	testConstruction();
	testCtls();

	std::printf("ValhallaSmoke: %d/%d checks passed\n", g_Checks - g_Failures, g_Checks);
	return g_Failures == 0 ? 0 : 1;
}
