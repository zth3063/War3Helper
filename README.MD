# War3Helper
Features have been added to the original open source program War3Trainer(which has since been taken down), in order to give players a better gaming experience, and perhaps to help learn the game's analysis
    
Thanks to the generous sharing and hard work of the original author, he has an admirable open source spirit
    
### Software overview
1.  War3Tariner: Modify unit data in War3, such as health, movement speed, attack, and most of the attributes you can see in the Unit editor
2.  MapHack: Show units, items, skills, resources, mirrors, bullets, and remove fog
3.  Other parts: Unit invincibility, instant kill, teleportation...
### What is the use of this software
1.  Get rid of the annoying numerical suppression and give you a better gaming experience
2.  Provide more information to help you understand the new map to play
3.  Call some trigger functions in the game, maybe you can have some interesting use? But I think abusing him would make your experience worse
4.  Learn some methods of reverse analysis, although I will tell you what I know, but I am a novice, you will not learn much useful
### Some tips you need to know
1.  Software for single play production. Although War3 is a state synchronous game, data modification does not affect other players,But if you use it in a multiplayer game, it may have bad results
2.  If the map you want to play only works on the game platform, you need to understand the platform's detection mechanism, or choose the alternative: modify the map and upload a new one to play
3.  The injection function has only been tested in the test game environment of the map Editor, and the KK platform will detect dll injection, please cherish your account
### What can you learn?
1.  Unit data structure of war3
2.  Some common function realization ideas
3.  Practice basic analytical methods
4.  A template for game injection (although all games now detect it)
### Before you start, you need to master these techniques
1.  Be able to debug 32-bit programs using tools such as x32dbg
2.  Can read assembly code and be familiar with most common commands
3.  Understand windows programming, such as message loops, hooks, stacks, dynamic link libraries
4.  Understand C language and use C to make simple windows library function calls
5.  Understand C#, able to write some simple functions, familiar with common windows controls, able to use c# designer for application interface layout
6.  The most important thing is the determination to overcome difficulties. You may encounter difficulties such as program errors, environment construction failures, promising ideas failure, etc., you can overcome these, and not only overcome these

# RemoteInjectDLL
  x86与x64通用的远程注入DLL、远程卸载DLL。
  原作者git地址:https://github.com/kahotv/RemoteInjectDLL
## 核心函数说明
  RemoteInjectDLL.h里
```cpp
//注入DLL并返回模块句柄（支持x64）
HMODULE	RemoteInjectDLL	(DWORD pid, const char* path);
BOOL	RemoteFreeDLL	(DWORD pid, HMODULE hModule);
```

## 依赖
  依赖Windows.h、STL(c++11及其以上)

## 代码目录结构

1. GameDLL 用于注入游戏的DLL,主要包含钩子回调函数和具体功能代码
2. loadDLL 测试时使用给的DLL,可以主动加载编写好的DLL
3. Remoteinject 注入器,用来注入应用程序,只需要更改dll名称和要注入的应用名称
4. RemoteinjectDLL 封装好的用来注入GameDLL,并通过windows消息机制,和注入后的GameDLL进行通信

## 编译
  以Release-x86或Release-x64编译，x64编译出的二进制文件以x64为结尾。
  32位EXE注入32位DLL到32位进程，64位的EXE注入64位的DLL到64位进程。
## 测试方式
 **注意：DLL和EXE必须同目录**
- 环境：Win10x64
- 注入器：RemoteInjectd.exe
- DLL：GameDLLd.dll
- 运行EXE，会自动注入到对应窗口名的进程中,注入器中有写好的枚举模块的函数,可以提供一些信息,默认未开启
- 测试消息默认会弹出一个弹出,作为提示.

# 如何使用?
1. 生成GameDLLd.dll
2. 生成RemoteInjectDLL.dll
3. 生成War3Helper
4. 放在同一个目录下,点击其他功能中的尝试注入