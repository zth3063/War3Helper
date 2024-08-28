#include <Windows.h>
#include <filesystem>
#include <iostream>
#include <map>
#include<TlHelp32.h>
#include <process.h>
#include <semaphore>
#include <thread>
std::map<std::string, int> OffsetMap;
LPCSTR WindowsClass = "Warcraft III";
int UnitSelectedAddressAddress = 0;
int address =0,UnitListAddress =0;//其实这个变量没必要作为全局变量的,但是全局变量可以直接在内联汇编中使用,我有点懒哈哈,这个变量在每个函数中都要重新赋值
int value = 0;//同样是内联汇编的时候方便使用
HHOOK g_hookId = 0;//太糟糕了,因为主线程在执行call的时候使用了线程私有变量,所有dll注入直接调用call获取的值有问题,我能想到的现在只有hook一下消息函数了
DWORD Msg_Call = RegisterWindowMessage("My_Inject_Msg_Call");//注册一个消息,消息名字不和其他程序使用的重复就行(这我怎么知道)
HWND h = 0;//窗口句柄
HANDLE hThread = 0;//设置钩子函数的线程句柄
HANDLE semaphoreHandle = CreateSemaphore(NULL, 0, 1, "isExitSemaphore");//信号量做状态判定,while(1)占用性能太多

/// <summary>
/// 得到模块的文件路径
/// </summary>
/// <param name="name">模块名</param>
/// <returns>文件路径</returns>
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
/// <summary>
/// 注入后模块句柄,函数地址初始化,单位地址获取,所有功能实现所需的公共基础,提取到这个函数
/// </summary>
/// <returns>不报异常的话返回true</returns>
boolean InjectInit() {
	std::string str123 = GetModuleFilePath(NULL) + "/" + "Game.dll";
	//测试获取dll文件的函数地址
	//获取DLL文件的模块句柄
	HMODULE hm = LoadLibrary(str123.c_str());
	if (hm == NULL) {
		DWORD errorCode = GetLastError();
		MessageBoxA(NULL, "dll加载失败", "TestDLL", MB_OK);
		return 0;
	}

	std::string str124 = GetModuleFilePath(NULL) + "/" + "kkapi_local_plugin.dll";
	//我以为正常游戏,也会加载这个dll呢,结果不是哈哈,只有测试会加载这个,白白逆向了
	HMODULE hm2=NULL;
	/*
	HMODULE hm2 = LoadLibrary(str124.c_str());
	if (hm2 == NULL) {
		DWORD errorCode = GetLastError();
		MessageBoxA(NULL, "dll加载失败", "TestDLL", MB_OK);
		return 0;
	}
	*/
	OffsetMap["GetTriggerUnit"] =  0x1E5E10+(int)hm;//不需要参数,返回unit类型的值,可以用int接受
	OffsetMap["GetRandomInt"] = 0x1E43E0 + (int)hm;//传入最小值,最大值
	OffsetMap["SetUnitInvulnerable"] = 0x1F7690 + (int)hm;//传入unit类型的值,和boolean类型的0或1,表示开启,关闭无敌
	OffsetMap["KillUnit"] = 0x1EFCD0 + (int)hm;
	OffsetMap["SetUnitX"] = 0x1F7C70 + (int)hm;
	OffsetMap["MouseX"] = 0x127190 + (int)hm2;
	OffsetMap["MouseY"] = 0x127194 + (int)hm2;
	OffsetMap["SetUnitY"] = 0x1F7CD0 + (int)hm;
	//获得选中列表中第一个单位的地址
	UnitListAddress = 0xBE4238 + (int)hm;//下面asm中才会用到
	__asm {
		push eax
		push ebx
		push ecx
		mov eax,UnitListAddress
		mov eax, dword ptr ds:[eax]
		mov bx,word ptr ds:[eax+0x28]
		add ebx,ebx
		add ebx,ebx
		mov ecx,dword ptr ds:[eax+0x58+ebx]
		mov ecx,dword ptr ds:[ecx+0x34]
		mov ecx,dword ptr ds:[ecx+0x1f0]
		add ecx,0x8
		mov UnitSelectedAddressAddress,ecx
		pop ecx
		pop ebx
		pop eax
	}
	return 1;
}

void SetUnitLocation(int status) {
	/*
		push ebp
		mov ebp, esp
		mov ecx, dword ptr ss : [ebp + 8]
		sub esp, C
		push esi
		call game.7A661550
		mov esi, eax
	*/
	int ret = 0;
	if (status == 0) {
		address = OffsetMap["SetUnitX"] + 0xF;
		value = OffsetMap["MouseX"];
	}
	if (status == 1) {
		address = OffsetMap["SetUnitY"] + 0xF;
		value = OffsetMap["MouseY"];
	}
	__asm {
		//这个寄存器平衡堆栈用,因为要把0x0和0x1都移出来,这里测试过程,就写成了0x1,改成0x0就是取消无敌
		push eax
		//函数传参
		mov eax, dword ptr ds : [value]
		push eax
		mov eax,esp
		push eax
		push 0x0
		lea eax, [retUnitLocalAddr]
		push eax

		push ebp
		mov ebp, esp
		sub esp, 0xC
		push esi
		mov eax, dword ptr ds : [UnitSelectedAddressAddress]//这个值要替换成要作用的单位地址,本来想用局部变量,但是内联汇编要求全局变量
		mov eax, [eax]
		//实际开始跳转
		jmp address
		//因为是截取了一部分,所以SetUnitInvulnerable函数出栈的操作,在jmp里面hah
		//函数出堆栈
		retUnitLocalAddr : pop eax
		pop eax
		pop eax
		//把恢复堆栈用的寄存器恢复
		pop eax
	}
}

void KillUnit() {
	int ret = 0;
	address = OffsetMap["KillUnit"] + 0xB;

	/*
		push ebp
		mov ebp,esp
		mov ecx,dword ptr ss:[ebp+8]
		call game.7A661550
		test eax,eax		跳到这里
	*/
	__asm {
		//这个寄存器平衡堆栈用
		push eax
		//函数传参
		push 0x0 //这函数没用
		//下面是模拟截取的SetUnitInvulnerable函数
		//必须是全局变量才能准确内联汇编,局部变量不改变ebp的话也可以,改变了ebp,之后使用的局部变量内联汇编会有bug
		//压入返回地址,假装已经跳转,这里把原来的call,改成push xxx,jmp xxx了
		lea eax, [retKillUnit]
		push eax

		push ebp
		mov ebp, esp
		mov eax, dword ptr ds : [UnitSelectedAddressAddress]//这个值要替换成要作用的单位地址,本来想用局部变量,但是内联汇编要求全局变量
		mov eax, [eax]
		//实际开始跳转
		jmp address
		//因为是截取了一部分,所以SetUnitInvulnerable函数出栈的操作,在jmp里面hah
		//函数出堆栈
		retKillUnit : pop eax
		//把恢复堆栈用的寄存器恢复
		pop eax
	}
}

/// <summary>
/// 设置单位无敌
/// </summary>
/// <param name="status">是否无敌</param>
void SetUnitInvulnerable(int status) {
	//设置单位无敌,原本函数传入unit,boolean,因为现在没办法弄到单位地址到unit的转换,我打算从中间截取,
	//从将unit地址转换为单位地址之后的程序,当做函数入口进行跳转
	//前面的堆栈平衡,手写汇编实现
	int ret = 0;
	address = OffsetMap["SetUnitInvulnerable"] + 0xC;//+0xC正好是unit转address函数的返回地址
	__asm {
		//这个寄存器平衡堆栈用,因为要把0x0和0x1都移出来,这里测试过程,就写成了0x1,改成0x0就是取消无敌
		push eax
		//函数传参
		push [status]
		push 0x0//这个操作数没有用到,本来是传入unit变量,现在这个值被截取替换了,但是为了堆栈平衡,还是要入栈一个东西
		//下面是模拟截取的SetUnitInvulnerable函数
		//必须是全局变量才能准确内联汇编,局部变量不改变ebp的话也可以,改变了ebp,之后使用的局部变量内联汇编会有bug
		//压入返回地址,假装已经跳转,这里把原来的call,改成push xxx,jmp xxx了
		lea eax,[retaddr]
		push eax

		push ebp
		mov ebp, esp
		push esi
		mov eax, dword ptr ds : [UnitSelectedAddressAddress]//这个值要替换成要作用的单位地址,本来想用局部变量,但是内联汇编要求全局变量
		mov eax, [eax]
		//实际开始跳转
		jmp address
		//因为是截取了一部分,所以SetUnitInvulnerable函数出栈的操作,在jmp里面hah
		//函数出堆栈
		retaddr : pop eax
		pop eax
		//把恢复堆栈用的寄存器恢复
		pop eax
	}
}
/// <summary>
/// (未完成,无法完成)得到触发单位
/// </summary>
/// <returns>返回单位unit值</returns>
int GetTriggerUnit() {
	int address = OffsetMap["GetTriggerUnit"];
	int ret = 0;
	__asm {
		mov dword ptr ds:[0x1B38284C],0x2 
		//盲猜,触发满足的时候,会把一个能唯一确定unit值的偏移放到这个地址
		/*
		mov eax,dword ptr ds:[ecx+eax*8+9C]
		mov eax,dword ptr ds:[eax+edx*4-4]
		这是源汇编函数,其中eax为0,edx为[0x1B38284C]的值,我试过几次同一个单位都是0x2
		又试了一次,的确最后获取的是unit的值,unit会被储存到一个地址中,最后保存到eax,猜测储存unit的函数就是触发函数,
		如果使用ce检测什么改写了这个地址的话,应该可以找到这个触发函数吧?
		不过这个0x2会不会改变呢?
			我又去看了看结果,我发现dword ptr ds:[eax+edx*4-4]地址附近又很多类似的值,而且我找到了我地图上其他两个单位的值,
			所以我猜测,0x2就是唯一标识该unit的值,可以把他当做触发器的触发部分根据unit变量生成的值,
			触发满足时写入到这里,可以看做偏移,唯一确定unit值的地址,那么一旦改变触发单位,这个值就会改变
			也就是说,如果我想把触发单位设置成指定的,比如说设置成选中单位,那么我就需要提前找到这个偏移,
			可是这个偏移的生成方式我并不知道,如果这个偏移是根据unit的值生成的,那对我来说没什么用
			而且这个函数和单位地址也没什么关系.
		*/
		call address
		mov ret,eax
	}
	//测试
	char buffer[20];
	sprintf_s(buffer, "%x", ret);
	LPCSTR result = buffer;
	MessageBoxA(h, result, "TestDLL", MB_OK);
	//返回unit的值,方便其他函数调用
	return ret;
}

//有效的获取主线程TID方式,对war3有效,理论上不是所有程序都有效
DWORD GetMainThreadId() {
	DWORD mainThreadId = 0;
	HANDLE hSnapshot = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
	if (hSnapshot != INVALID_HANDLE_VALUE) {
		THREADENTRY32 te;
		te.dwSize = sizeof(THREADENTRY32);
		if (Thread32First(hSnapshot, &te)) {
			do {
				if (te.dwSize >= FIELD_OFFSET(THREADENTRY32, th32OwnerProcessID) + sizeof(te.th32OwnerProcessID)) {
					if (te.th32OwnerProcessID == GetCurrentProcessId()) {
						mainThreadId = te.th32ThreadID;
						break;
					}
				}
			} while (Thread32Next(hSnapshot, &te));
		}
		CloseHandle(hSnapshot);
	}
	return mainThreadId;
}

//撤销Hook
void UnHook() {
	if (!UnhookWindowsHookEx(g_hookId)) {
		char buffer[100];
		sprintf_s(buffer, "%lu", GetLastError());
		LPCSTR lpcstrValue = buffer;
		MessageBoxA(h, lpcstrValue, "TestDLL", MB_OK);
	}
}

//回调函数,这个回调函数的参数传递,我也觉得很离谱,
//但是前辈们都是用这种格式写的,强制转换指针,在获取指针指向结构体的属性
LRESULT CALLBACK Func_Call(int nCode, WPARAM wParam, LPARAM lParam) {
	CWPSTRUCT* ptr = reinterpret_cast<CWPSTRUCT*>(lParam);
	if (nCode >= 0) {
		if (ptr->message == Msg_Call) {
			switch (ptr->wParam){
			case 0:
				//保留,测试使用
				MessageBoxA(h, "测试结果窗口", "Test", MB_OK);
				break;
			case 1:
				//单位无敌和取消无敌
				SetUnitInvulnerable(ptr->lParam);
				break;
			case 2:
				//秒杀单位
				KillUnit();
				break;
			case 3:
				//TODO 之前的思路有问题,现在这里无效
				//瞬移,设置X和Y
				//SetUnitLocation(0);
				//SetUnitLocation(1);
				break;
			default:
				//发送未定义消息,自动撤销hook,退出设置回调钩子的线程,方便dll进行卸载
				ReleaseSemaphore(semaphoreHandle, 1, NULL);
				break;
			}
			return 0;
		}
	}
	return CallNextHookEx(g_hookId, nCode, wParam, lParam);
}

//下钩函数
boolean HookInit() {
	h = FindWindowA(WindowsClass, 0);
	if (h == 0) {
		MessageBoxA(h, "未找到窗口", "TestDLL", MB_OK);
		return 0;
	}
	//下面这个方法对有道词典是有效的,但是对魔兽无效...甚至获取的线程都不存在哈哈哈
	//DWORD mainTid = GetWindowThreadProcessId(h, NULL);
	//换一个方法,此方法对war3有效
	DWORD mainTid = GetMainThreadId();
	if (mainTid == 0) {
		MessageBoxA(h, "未找到主线程", "TestDLL", MB_OK);
		return 0;
	}
	g_hookId = SetWindowsHookExA(WH_CALLWNDPROC, Func_Call, NULL, mainTid);
	if (g_hookId == NULL) {
		MessageBoxA(h, "回调钩子设置失败", "TestDLL", MB_OKCANCEL);
		return 0;
	}
	return 1;
}

//负责设置主线程回调钩子的额外线程
unsigned int __stdcall  MessageHookThreadProc(LPVOID lpParam)
{
	if (!HookInit()) {
		MessageBoxA(h, "Hook注入失败", "TestDLL", MB_OK);
	}
	//阻塞等待退出
	//据我之前的了解,设置了回调钩子,就可以退出了而且不影响钩子执行,但是实际并不是...这个线程退出后,钩子无法销毁,且不会正常运行,所以阻塞吧
	WaitForSingleObject(semaphoreHandle, INFINITE);
	//关闭之前,卸载钩子,释放信号量
	UnHook();
	CloseHandle(semaphoreHandle);
	return 1;
}

BOOL APIENTRY DllMain( HMODULE hModule,
                       DWORD  ul_reason_for_call,
                       LPVOID lpReserved
                     )
{
    switch (ul_reason_for_call)
    {
		case DLL_PROCESS_ATTACH:
		{
			//远程注入之后,在此处创建线程,不会立刻执行,知道当前代码块执行完毕,且当前线程包括子线程无法设置消息循环
			h = FindWindowA(WindowsClass, 0);
			if (h == 0) {
				MessageBoxA(h, "未找到窗口", "TestDLL", MB_OK);
				return 0;
			}
			//一些war3变量初始化,主要是获取选中单位地址的地址(这是个列表,但是只拿了第一个值)
			InjectInit();
			
			//开启一个注册回调钩子的子线程
			unsigned int threadId;
			hThread = (HANDLE)_beginthreadex(NULL, 0, MessageHookThreadProc, NULL, 0, &threadId);
			if (hThread == 0)
			{
				MessageBoxA(h, "子线程创建失败", "TestDLL", MB_OK);
				//mutexLock.unlock();
				break;
			}

			break;
		}
		case DLL_THREAD_ATTACH://远程注入的线程也会触发ATTACH和DETACH
			break;
		case DLL_THREAD_DETACH:
			break;
		case DLL_PROCESS_DETACH:
		{
			//未理解原理,如果回调钩子函数没有退出或者回调钩子的线程没有退出,
			//则无法进入这个条件,但是我本来打算在这个条件里面进行回调函数的退出的
			//没能猜到可能的原因,CreateRemoteThread是windows提供了,我并不知道原理

			break;
		}
    }
    return TRUE;
}

