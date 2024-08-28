//将注入器改写为dll
#include"RemoteInjectDLL.h"
#include <Windows.h>
#include <iostream>
#include <string>
#include <filesystem>
#include <Windows.h>
#include <tlhelp32.h>

#ifdef _WIN64
#define DLL_NAME		"GAMEDLL64.dll"
#else
#define DLL_NAME		"GAMEDLLd.dll"
#endif
#define EXE_NAME L"War3.exe"
#define WINDOWS_CLASS "Warcraft III"
#define DEFINATION_MESSAGE "My_Inject_Msg_Call"

extern "C" {
	__declspec(dllexport) boolean injectDLL();
	__declspec(dllexport) boolean refresh();
	__declspec(dllexport) boolean removeDLL();
	__declspec(dllexport) boolean SendMsg(WPARAM funcType, LPARAM paramPoint);
}

DWORD pid;//进程pid
HMODULE h;//模块句柄
HWND hwnd;//窗口句柄
DWORD Msg_Call = RegisterWindowMessage(DEFINATION_MESSAGE);//自定义的消息

std::string	GetModuleFilePath(const char* name)
{
	char tmppath[MAX_PATH];
	HMODULE h = GetModuleHandleA(name);

	if (0 != GetModuleFileNameA(h, tmppath, sizeof(tmppath)))
	{
		std::filesystem::path path = tmppath;

		if (std::filesystem::is_regular_file(path))
		{
			return path.parent_path().generic_string();
		}
	}
	return "";
}

DWORD GetProcessIdByName(const std::wstring& processName) {
	HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
	if (snapshot == INVALID_HANDLE_VALUE) {
		return 0;
	}

	PROCESSENTRY32W processEntry;
	processEntry.dwSize = sizeof(processEntry);

	if (Process32FirstW(snapshot, &processEntry)) {
		do {
			if (processName == processEntry.szExeFile) {
				CloseHandle(snapshot);
				return processEntry.th32ProcessID;
			}
		} while (Process32NextW(snapshot, &processEntry));
	}

	CloseHandle(snapshot);
	return 0;
}

boolean init() {
    if (!AdjustSeltTokenPrivileges()) {
        MessageBox(NULL, "权限不足,请以管理员身份运行", "错误信息", MB_OK | MB_ICONINFORMATION);
        return false;
    }
    pid = GetProcessIdByName(EXE_NAME);
	if (pid == 0) {
		MessageBox(NULL, "获取进程ID失败", "错误信息", MB_OK | MB_ICONINFORMATION);
		return false;
	}
	hwnd = FindWindowA(WINDOWS_CLASS, 0);
	if (hwnd == NULL) {
		MessageBox(NULL, "获取窗口句柄失败", "错误信息", MB_OK | MB_ICONINFORMATION);
		return false;
	}
	return true;
}

boolean injectDLL() {
	//如果默认init失败的话,这里重新初始化一次
	if (hwnd == NULL) {
		if (!init()) {
			return false;
		}
	}
	std::string path = GetModuleFilePath(NULL) + "/" + DLL_NAME;
	h = RemoteInjectDLL(pid, path.c_str());
	if (h == 0) {
		MessageBox(NULL, "注入DLL失败", "错误信息", MB_OK | MB_ICONINFORMATION);
		return false;
	}
	return true;
}

//一键初始化,并注入
boolean refresh() {
	if (init())
		if (injectDLL())
			return true;
	return false;
}

boolean removeDLL() {
	//发送卸载消息,先结束注入到游戏内的消息循环
	SendMessageA(hwnd, Msg_Call, 100, 0);
	Sleep(1000);
	//卸载DLL,这个函数有没有效果,只有windows知道
	if (!RemoteFreeDLL(pid, h)) {
		MessageBox(NULL, "卸载DLL失败", "错误信息", MB_OK | MB_ICONINFORMATION);
		return false;
	}
	return true;
}

//自定义的函数类型(int),参数结构体
boolean SendMsg(WPARAM funcType, LPARAM paramPoint) {
	if (!SendMessage(hwnd, Msg_Call, funcType, paramPoint))
		return false;
	return true;
}

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        init();
		break;
    case DLL_THREAD_ATTACH:
		break;
    case DLL_THREAD_DETACH:
		break;
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}