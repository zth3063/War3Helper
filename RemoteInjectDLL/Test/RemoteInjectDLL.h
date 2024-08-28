#pragma once
#include <Windows.h>
#include <iostream>
#include <string>
#include <filesystem>
#include <Windows.h>
#include <tlhelp32.h>
//注入DLL并返回模块句柄
HMODULE	RemoteInjectDLL	(DWORD pid, const char* path);
BOOL	RemoteFreeDLL	(DWORD pid, HMODULE hModule);
void	EnumModulesForWindow(const char* ch);
BOOL	AdjustSeltTokenPrivileges();