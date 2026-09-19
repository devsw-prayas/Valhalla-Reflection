#pragma once
#include "ValhallaCore.h"
#include "ValhallaDescriptors.h"

namespace Valhalla {

	ValhallaCore_API void RegisterType(uint64_t p_DllHash, const TypeDescriptor* p_Type);
	ValhallaCore_API const TypeDescriptor* GetType(uint64_t p_DllHash, uint64_t p_TypeHash);

}
