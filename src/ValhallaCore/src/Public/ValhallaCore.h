#pragma once
#include <iostream>
#include <string>
#include <vector>
#include <memory>

#ifdef ValhallaCore_EXPORTS
#  define ValhallaCore_API __declspec(dllexport)
#else
#  define ValhallaCore_API __declspec(dllimport)
#endif

ValhallaCore_API void Init();
