#include "RemoteInjectDLL.h"
#include <stdio.h>
#include <filesystem>
#include <psapi.h>
#pragma pack(push,1)
#ifdef _WIN64
#define InjectDLLShllCode InjectDLLShllCode64
struct InjectDLLShllCode64
{
	BYTE		_1[2];
	SIZE_T		addr_dll_name;
	BYTE		_2[3];
	SIZE_T		addr_loadlibrarya;
	BYTE		_3[4];
	SIZE_T		addr_ret;
	BYTE		_4[6];
	SIZE_T		addr_exitthread;
	BYTE		_5[3];
	InjectDLLShllCode64() :
		_1{ 0x48,0xB9 },
		_2{ 0x51,0x48,0xB8 },
		_3{ 0xFF,0xD0,0x48,0XA3 },
		_4{ 0x48,0x31,0xC9,0x51,0x48,0xB8 },
		_5{ 0xFF,0xD0,0xCC },
		addr_dll_name(0),
		addr_loadlibrarya(0),
		addr_ret(0),
		addr_exitthread(0)
	{
		/*
		mov		rcx,addr_dll_name
		push	rcx
		mov		rax,addr_loadlibrarya
		call	rax
		mov		qword ptr [addr_ret],rax
		xor		rcx,rcx
		push	rcx
		mov		rax,addr_exitthread
		call	rax
		int3
		*/
	}
};
#else
#define InjectDLLShllCode InjectDLLShllCode32
struct InjectDLLShllCode32
{
	BYTE		_1[1];
	SIZE_T		addr_dll_name;
	BYTE		_2[1];
	SIZE_T		addr_loadlibrarya;
	BYTE		_3[3];
	SIZE_T		addr_ret;
	BYTE		_4[3];
	SIZE_T		addr_exitthread;
	BYTE		_5[3];
	InjectDLLShllCode32() :
		_1{ 0x68 },
		_2{ 0xB8 },
		_3{ 0xFF,0xD0,0xA3 },
		_4{ 0x6A,0x00,0xB8 },
		_5{ 0xFF,0xD0,0xCC },
		addr_dll_name(0),
		addr_loadlibrarya(0),
		addr_ret(0),
		addr_exitthread(0)
	{
		/*
		push	addr_dll_name
		mov		eax,addr_loadlibrarya
		call	eax
		mov		dword ptr[addr_ret],eax
		push	0
		mov		eax,addr_exitthread
		call	eax
		int3
		*/
	}
};
#endif
#pragma pack(pop)

//获取DLL函数
SIZE_T	GetDllFunc(const char* dllName, const char* funcName)
{
	HMODULE h = LoadLibraryA(dllName);
	if (h != 0)
	{
		return (SIZE_T)GetProcAddress(h, funcName);
	}
	return 0;
}

//注入DLL并返回模块句柄
HMODULE RemoteInjectDLL(DWORD pid, const char* path)
{

	HMODULE hModule = 0;
	HANDLE hProcess = 0;
	HANDLE hThread = 0;
	SIZE_T pRemoteBuf = 0;
	SIZE_T pathLen = 0;
	do
	{
		if (path == NULL)
		{
			printf("DLL路径为空\n");
			break;
		}
		pathLen = strlen(path);

		if (pathLen == 0)
		{
			printf("DLL路径为空\n");
			break;
		}

		if (!std::filesystem::is_regular_file(path))
		{
			printf("DLL文件不存在\n");
			break;
		}

		hProcess = OpenProcess(PROCESS_ALL_ACCESS, FALSE, pid);
		if (hProcess == 0)
		{
			printf("打开进程[%d]失败，错误代码%d\n", pid, GetLastError());
			break;
		}

		pRemoteBuf = (SIZE_T)VirtualAllocEx(hProcess, NULL, 0x1000, MEM_COMMIT, PAGE_EXECUTE_READWRITE);  // 在目标进程空间中申请内存

		//pRemoteBuf:
		//+0=返回值;+0x10=参数;+0x100=shellcode

		if (pRemoteBuf <= 0)
		{
			printf("申请内存失败，错误代码%d\n",GetLastError());
			break;
		}

		//准备好相关函数和参数
		SIZE_T pLoadLibraryA = GetDllFunc("kernel32.dll", "LoadLibraryA");
		SIZE_T pExitThread = GetDllFunc("kernel32.dll", "ExitThread");


		//构造shellcode
		InjectDLLShllCode sc;

		sc.addr_dll_name = pRemoteBuf + 0x10;
		sc.addr_exitthread = pExitThread;
		sc.addr_loadlibrarya = pLoadLibraryA;
		sc.addr_ret = pRemoteBuf;

		//写入内存	返回值	置0
		WriteProcessMemory(hProcess, (void*)(pRemoteBuf + 0x0), &hModule, sizeof(hModule), NULL);
		//写入内存	参数	DLL路径
		WriteProcessMemory(hProcess, (void*)(pRemoteBuf + 0x10), path, pathLen, NULL);
		//写入内存	shellcode 
		WriteProcessMemory(hProcess, (void*)(pRemoteBuf + 0x100), &sc, sizeof(sc), NULL);

		//创建线程
		hThread = CreateRemoteThread(hProcess, NULL, 0, (LPTHREAD_START_ROUTINE)(pRemoteBuf + 0x100), NULL, 0, NULL);

		if (hThread == 0)
		{
			printf("创建远程线程失败,错误代码%d\n", GetLastError());
			break;
		}

		//等待线程结束
		WaitForSingleObject(hThread, INFINITE);

		//读取返回值
		if (0 == ReadProcessMemory(hProcess, (void*)pRemoteBuf, &hModule, sizeof(hModule), NULL))
		{
			hModule = 0;
		}

	} while (false);

	if (hThread != 0)
		CloseHandle(hThread);

	if (pRemoteBuf != 0)
		VirtualFreeEx(hProcess, (void*)pRemoteBuf, 0, MEM_FREE);

	if (hProcess != 0)
		CloseHandle(hProcess);
	return hModule;
}

//卸载指定进程的指定模块
BOOL RemoteFreeDLL(DWORD pid,HMODULE hModule)
{
	HANDLE hProcess = 0;
	HANDLE hThread = 0;
	do
	{
		if (hModule == 0)
		{
			printf("模块句柄为空\n");
			break;
		}

		hProcess = OpenProcess(PROCESS_ALL_ACCESS, FALSE, pid);
		if (INVALID_HANDLE_VALUE == hProcess)
		{
			printf("打开进程失败，错误代码%d\n",GetLastError());
			break;
		}
		//准备好相关函数和参数
		SIZE_T pFreeLibrary = GetDllFunc("kernel32.dll", "FreeLibrary");

		//创建线程，参数就是模块句柄
		hThread = CreateRemoteThread(hProcess, NULL, 0, (LPTHREAD_START_ROUTINE)(pFreeLibrary), hModule, 0, NULL);

		if (hThread != 0)
		{
			//等待线程结束
			WaitForSingleObject(hThread, INFINITE);
			DWORD dwExitCode;
			if (GetExitCodeThread(hThread, &dwExitCode)) {
				printf("卸载线程退出代码:%d\n", dwExitCode);
			}
			break;
		}
		printf("卸载线程成功返回\n");

	} while (false);

	if (hThread != 0)
		CloseHandle(hThread);

	if (hProcess != 0)
		CloseHandle(hProcess);
	return TRUE;
}

BOOL	AdjustSeltTokenPrivileges()
{
	HANDLE hToken;
	TOKEN_PRIVILEGES tp;
	LUID luid;

	if (FALSE == OpenProcessToken(
		GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, &hToken))
	{
		return FALSE;
	}

	if (!LookupPrivilegeValueA(NULL, "SeDebugPrivilege", &luid))
	{
		return FALSE;
	}

	tp.PrivilegeCount = 1;
	tp.Privileges[0].Luid = luid;
	tp.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;

	if (!AdjustTokenPrivileges(hToken, FALSE, &tp, sizeof(TOKEN_PRIVILEGES), NULL, NULL))
	{
		return FALSE;
	}

	return TRUE;
}

void EnumModulesForWindow(const char* windowClass) {
	HWND hWnd = FindWindowA(windowClass, NULL);
	if (hWnd == NULL) {
		std::cout << "Window not found." << std::endl;
		return;
	}

	DWORD processId;
	GetWindowThreadProcessId(hWnd, &processId);

	HANDLE hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, processId);
	if (hProcess == NULL) {
		std::cout << "Failed to open process." << std::endl;
		return;
	}

	HMODULE hMods[1024];
	DWORD cbNeeded;
	if (EnumProcessModules(hProcess, hMods, sizeof(hMods), &cbNeeded)) {
		for (int i = 0; i < (cbNeeded / sizeof(HMODULE)); i++) {
			TCHAR szModName[MAX_PATH];
			if (GetModuleFileNameEx(hProcess, hMods[i], szModName, sizeof(szModName) / sizeof(TCHAR))) {
				std::wcout << szModName << std::endl;
			}
		}
	}

	CloseHandle(hProcess);
}