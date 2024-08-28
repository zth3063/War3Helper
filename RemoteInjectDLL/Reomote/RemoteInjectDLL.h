#pragma once
#include <Windows.h>
//注入DLL并返回模块句柄
HMODULE	RemoteInjectDLL	(DWORD pid, const char* path);
BOOL	RemoteFreeDLL	(DWORD pid, HMODULE hModule);

BOOL	AdjustSeltTokenPrivileges();