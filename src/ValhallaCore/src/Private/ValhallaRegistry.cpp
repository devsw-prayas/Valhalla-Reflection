#include "ValhallaRegistry.h"
#include <vector>

namespace Valhalla {

	namespace {
		struct Entry {
			uint64_t m_DllHash;
			uint64_t m_TypeHash;
			const TypeDescriptor* m_Type;
		};

		std::vector<Entry>& registry() {
			static std::vector<Entry> s_Entries;
			return s_Entries;
		}
	}

	void RegisterType(uint64_t p_DllHash, const TypeDescriptor* p_Type) {
		registry().push_back({ p_DllHash, p_Type->m_Hash, p_Type });
	}

	const TypeDescriptor* GetType(uint64_t p_DllHash, uint64_t p_TypeHash) {
		for (const Entry& entry : registry()) {
			if (entry.m_DllHash == p_DllHash && entry.m_TypeHash == p_TypeHash) return entry.m_Type;
		}
		return nullptr;
	}

}
