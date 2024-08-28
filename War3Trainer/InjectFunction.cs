using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
namespace War3Trainer
{
    //一个工具类,负责加载dll,加载注入功能的配置文件
    // TODO:
    // 1. 加载配置
    // 2. 
    internal class InjectFunction
    {
        [DllImport("RemoteInjectDLL.dll",CallingConvention = CallingConvention.Cdecl)]
        public static extern bool injectDLL();

        [DllImport("RemoteInjectDLL.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool refresh();

        [DllImport("RemoteInjectDLL.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool removeDLL();

        [DllImport("RemoteInjectDLL.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SendMsg(IntPtr funcType, IntPtr paramPoint);

        public static string[] getInjectConfig()
        {
            string[] a = new string[20];

            return a;
        }
    }
}
