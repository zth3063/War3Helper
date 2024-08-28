// loadDLL.cpp : 定义应用程序的入口点。
//

#include "framework.h"
#include "loadDLL.h"
#include <Windows.h>
#include <iostream>
#include <string>
#include <filesystem>
#include <Windows.h>
#include <tlhelp32.h>
#define MAX_LOADSTRING 100

// 全局变量:

typedef bool (*initDLL)();//使用导出函数的函数指针
typedef bool (*Send)(WPARAM funcType, LPARAM paramPoint);

HINSTANCE hInst;                                // 当前实例
WCHAR szTitle[MAX_LOADSTRING];                  // 标题栏文本
WCHAR szWindowClass[MAX_LOADSTRING];            // 主窗口类名
HMODULE hDll; //加载dll的句柄
// 此代码模块中包含的函数的前向声明:
ATOM                MyRegisterClass(HINSTANCE hInstance);
BOOL                InitInstance(HINSTANCE, int);
LRESULT CALLBACK    WndProc(HWND, UINT, WPARAM, LPARAM);
INT_PTR CALLBACK    About(HWND, UINT, WPARAM, LPARAM);

//一些工具函数
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

//提权
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


//主函数
int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
                     _In_opt_ HINSTANCE hPrevInstance,
                     _In_ LPWSTR    lpCmdLine,
                     _In_ int       nCmdShow)
{
    UNREFERENCED_PARAMETER(hPrevInstance);
    UNREFERENCED_PARAMETER(lpCmdLine);

    // TODO: 在此处放置代码。

    // 初始化全局字符串
    LoadStringW(hInstance, IDS_APP_TITLE, szTitle, MAX_LOADSTRING);
    LoadStringW(hInstance, IDC_LOADDLL, szWindowClass, MAX_LOADSTRING);
    MyRegisterClass(hInstance);

    // 执行应用程序初始化:
    if (!InitInstance (hInstance, nCmdShow))
    {
        return FALSE;
    }

    HACCEL hAccelTable = LoadAccelerators(hInstance, MAKEINTRESOURCE(IDC_LOADDLL));

    MSG msg;

    // 主消息循环:
    while (GetMessage(&msg, nullptr, 0, 0))
    {
        if (!TranslateAccelerator(msg.hwnd, hAccelTable, &msg))
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
    }

    return (int) msg.wParam;
}



//
//  函数: MyRegisterClass()
//
//  目标: 注册窗口类。
//
ATOM MyRegisterClass(HINSTANCE hInstance)
{
    WNDCLASSEXW wcex;

    wcex.cbSize = sizeof(WNDCLASSEX);

    wcex.style          = CS_HREDRAW | CS_VREDRAW;
    wcex.lpfnWndProc    = WndProc;
    wcex.cbClsExtra     = 0;
    wcex.cbWndExtra     = 0;
    wcex.hInstance      = hInstance;
    wcex.hIcon          = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_LOADDLL));
    wcex.hCursor        = LoadCursor(nullptr, IDC_ARROW);
    wcex.hbrBackground  = (HBRUSH)(COLOR_WINDOW+1);
    wcex.lpszMenuName   = MAKEINTRESOURCEW(IDC_LOADDLL);
    wcex.lpszClassName  = szWindowClass;
    wcex.hIconSm        = LoadIcon(wcex.hInstance, MAKEINTRESOURCE(IDI_SMALL));

    return RegisterClassExW(&wcex);
}

//
//   函数: InitInstance(HINSTANCE, int)
//
//   目标: 保存实例句柄并创建主窗口
//
//   注释:
//
//        在此函数中，我们在全局变量中保存实例句柄并
//        创建和显示主程序窗口。
//
BOOL InitInstance(HINSTANCE hInstance, int nCmdShow)
{
   hInst = hInstance; // 将实例句柄存储在全局变量中

   HWND hWnd = CreateWindowW(szWindowClass, szTitle, WS_OVERLAPPEDWINDOW,
      CW_USEDEFAULT, 0, CW_USEDEFAULT, 0, nullptr, nullptr, hInstance, nullptr);

   if (!hWnd)
   {
      return FALSE;
   }
   const char CLASS_NAME[] = "MyWindowClass";
   WNDCLASS wc = { };
   wc.lpfnWndProc = WndProc;
   wc.hInstance = GetModuleHandle(NULL);
   wc.lpszClassName = CLASS_NAME;
   wc.style = CS_HREDRAW | CS_VREDRAW;
   RegisterClass(&wc);

   const char CLASS_NAME2[] = "MyWindowClass2";
   WNDCLASS wc2 = { };
   wc2.lpfnWndProc = WndProc;
   wc2.hInstance = GetModuleHandle(NULL);
   wc2.lpszClassName = CLASS_NAME2;
   wc2.style = CS_HREDRAW | CS_VREDRAW;
   RegisterClass(&wc2);

   HWND button = CreateWindow(
       "BUTTON",
       "refrsh()",
       WS_TABSTOP | WS_VISIBLE | WS_CHILD | BS_PUSHBUTTON,
       10, 10, 100, 30,
       hWnd,
       (HMENU)1,
       wc.hInstance,
       NULL
   );
   HWND button2 = CreateWindow(
       "BUTTON",
       "UnitInvincible()",
       WS_TABSTOP | WS_VISIBLE | WS_CHILD | BS_PUSHBUTTON,
       120, 10, 100, 30,
       hWnd,
       (HMENU)2,
       wc.hInstance,
       NULL
   );


   ShowWindow(hWnd, nCmdShow);
   UpdateWindow(hWnd);

   return TRUE;
}

void OnButtonClick()
{
    if (AdjustSeltTokenPrivileges()) {
        MessageBox(NULL, "提权成功", "Info", MB_OK);
    }

    hDll = LoadLibrary("RemoteInjectDLL.dll");
    if (hDll == NULL)
    {
        std::cerr << "Failed to load DLL." << std::endl;
        return ;
    }

    initDLL initdll = reinterpret_cast<initDLL>(GetProcAddress(hDll, "refresh"));
    
    if (initdll()) {
        MessageBox(NULL, "调用成功", "Info", MB_OK);
        return;
    }
    MessageBox(NULL, "调用失败", "Info", MB_OK);
}
void OnButtonClick2()
{
    Send SendMyMsg = reinterpret_cast<Send>(GetProcAddress(hDll, "SendMsg"));
    if (!SendMyMsg(1, 1)) {
        MessageBox(NULL, "发送成功", "Info", MB_OK);
        return;
    }
    MessageBox(NULL, "调用失败", "Info", MB_OK);
}

//
//  函数: WndProc(HWND, UINT, WPARAM, LPARAM)
//
//  目标: 处理主窗口的消息。
//
//  WM_COMMAND  - 处理应用程序菜单
//  WM_PAINT    - 绘制主窗口
//  WM_DESTROY  - 发送退出消息并返回
//
//
LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    switch (message)
    {
    case WM_COMMAND:
        {
            int wmId = LOWORD(wParam);
            // 分析菜单选择:
            switch (wmId)
            {
            case 1:
                //按钮refresh被点击
                OnButtonClick();
                break;
            case 2:
                //按钮UnitInvincible被点击
                OnButtonClick2();
                break;
            case IDM_ABOUT:
                DialogBox(hInst, MAKEINTRESOURCE(IDD_ABOUTBOX), hWnd, About);
                break;
            case IDM_EXIT:
                FreeLibrary(hDll);
                DestroyWindow(hWnd);
                break;
            default:
                return DefWindowProc(hWnd, message, wParam, lParam);
            }
        }
        break;
    case WM_PAINT:
        {
            PAINTSTRUCT ps;
            HDC hdc = BeginPaint(hWnd, &ps);
            // TODO: 在此处添加使用 hdc 的任何绘图代码...
            EndPaint(hWnd, &ps);
        }
        break;
    case WM_DESTROY:
        PostQuitMessage(0);
        break;
    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
    }
    return 0;
}

// “关于”框的消息处理程序。
INT_PTR CALLBACK About(HWND hDlg, UINT message, WPARAM wParam, LPARAM lParam)
{
    UNREFERENCED_PARAMETER(lParam);
    switch (message)
    {
    case WM_INITDIALOG:
        return (INT_PTR)TRUE;

    case WM_COMMAND:
        if (LOWORD(wParam) == IDOK || LOWORD(wParam) == IDCANCEL)
        {
            EndDialog(hDlg, LOWORD(wParam));
            return (INT_PTR)TRUE;
        }
        break;
    }
    return (INT_PTR)FALSE;
}
