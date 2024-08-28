using System;

namespace War3Trainer
{
    static class War3Common
    {
        // Game memory init
        public static void GetGameMemory(
            GameContext gameContext,
            ref NewChildrenEventArgs gameAddress)
        {
            using (WindowsApi.ProcessMemory mem = new WindowsApi.ProcessMemory(gameContext.ProcessId))
            {
                gameAddress.ThisGameAddress = mem.ReadUInt32((IntPtr)gameContext.ThisGameAddress);
                if (gameAddress.ThisGameAddress != 0)
                {
                    gameAddress.ThisGameMemoryAddress = mem.ReadUInt32((IntPtr)unchecked(gameAddress.ThisGameAddress + 0xC));
                    if (gameAddress.ThisGameMemoryAddress == 0xFFFFFFFF)
                        gameAddress.ThisGameMemoryAddress = 0;
                }

                if (gameAddress.ThisGameAddress == 0
                    || gameAddress.ThisGameMemoryAddress == 0)
                {
                    gameAddress.ThisGameMemoryAddress = 0;
                    gameAddress.ThisUnitAddress = 0;
                    gameAddress.AttackAttributesAddress = 0;
                    gameAddress.HeroAttributesAddress = 0;
                }
            }
        }

        // Game memory extract algorithm 0 
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "gameContext")]
        public static UInt32 ReadFromGameMemory(
            WindowsApi.ProcessMemory mem, GameContext gameContext, NewChildrenEventArgs gameAddress,
            Int32 index)
        {
            System.Diagnostics.Debug.Assert(index >= 0);

            if (gameAddress.ThisGameMemoryAddress == 0)
                return 0;

            return mem.ReadUInt32((IntPtr)unchecked(gameAddress.ThisGameMemoryAddress + index * 8 + 4));
        }

        // Game memory extract algorithm 1
        // Used in Intelligence
        public static UInt32 ReadGameValue1(
            WindowsApi.ProcessMemory mem, GameContext gameContext, NewChildrenEventArgs gameAddress,
            Int32 index)
        {
            if (gameAddress.ThisGameMemoryAddress == 0)
                return 0;

            return unchecked(0x78 + ReadFromGameMemory(
                mem, gameContext, gameAddress,
                index));
        }

        // Game memory extract algorithm 2
        // Used in MoveSpeed
        public static UInt32 ReadGameValue2(
            WindowsApi.ProcessMemory mem, GameContext gameContext, NewChildrenEventArgs gameAddress,
            Int32 index)
        {
            if (gameAddress.ThisGameMemoryAddress == 0)
                return 0;

            UInt32 tmpValue = ReadFromGameMemory(
                mem, gameContext, gameAddress,
                index);
            if (0 == mem.ReadUInt32((IntPtr)unchecked(tmpValue + 0x20)))
            {
                return mem.ReadUInt32((IntPtr)unchecked(tmpValue + 0x54));
            }
            else
            {
                System.Windows.Forms.MessageBox.Show(
                    "Bug detected in ReadGameValue2().",
                    "Bug check");
                // Copy sub_6F468A20() again, set breakpoint at 6F0776F6
                return 0;
            }
        }
    }
}

// How to get (WindowsApi.ProcessMemory mem):
// using (WindowsApi.ProcessMemory mem = new WindowsApi.ProcessMemory(GameContext.ProcessID))

