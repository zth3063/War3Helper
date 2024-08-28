// Test.cpp : 此文件包含 "main" 函数。程序执行将在此处开始并结束。
//

#include <iostream>
#include <string>
#include <filesystem>
#include <Windows.h>
#include "RemoteInjectDLL.h"
#include <tlhelp32.h>
#ifdef _WIN64
#define DLL_NAME		"GAMEDLL64.dll"
#else
#define DLL_NAME		"GAMEDLLd.dll"
#endif
#define EXE_NAME L"War3.exe"
#define WINDOWS_CLASS "Warcraft III"
#define DEFINATION_MESSAGE "My_Inject_Msg_Call"
//主要的流程确定改下来了
/*
首先注入dll,由dll在程序内存中加载要实现的功能,
构建好和注入器通信的回调钩子函数
通过windows的消息循环,由注入器向回调函数所在的进程发信自定义的消息
达到开启,关闭的功能
最后封装一个UI
*/
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

int main()
{
	DWORD pid;
	HMODULE h;
	//提权
	std::cout << "提权:" << (AdjustSeltTokenPrivileges() ? "成功" : "失败") << std::endl;
	//枚举进程所有模块
	//EnumModulesForWindow("Warcraft III");

	//获取pid
	pid = GetProcessIdByName(EXE_NAME);

	//取EXE目录
	std::string path = GetModuleFilePath(NULL) + "/" + DLL_NAME;
	std::cout << "注入 " << path << " 到进程 " << pid << std::endl;
	h = RemoteInjectDLL(pid, path.c_str());
	std::cout << "模块句柄:" << h << std::endl;
	HWND hwnd = FindWindowA(WINDOWS_CLASS, 0);
	if (hwnd == NULL) {
		printf_s("未找到对应窗口");
		goto a;
	}
	std::cout << "任意键发送测试消息" << std::endl;
	system("pause");
	SendMessageA(hwnd, Msg_Call, 0, 0);

	std::cout << "任意键发送卸载消息" << std::endl;
	system("pause");
	SendMessageA(hwnd, Msg_Call, 100, 0);
	
	std::cout << "任意键卸载DLL" << std::endl;
	system("pause");

	a: RemoteFreeDLL(pid, h);

	std::cout << "卸载DLL完毕" << std::endl;

	system("pause");
}
