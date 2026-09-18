#pragma once

#ifdef ValhallaCore_EXPORTS
#  define ValhallaCore_API __declspec(dllexport)
#else
#  define ValhallaCore_API __declspec(dllimport)
#endif
