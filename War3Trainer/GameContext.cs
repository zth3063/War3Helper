using System;
using System.Diagnostics;

namespace War3Trainer
{
    class GameContext
    {
        public int ProcessId                  { get; private set; }
        public string ProcessVersion          { get; private set; }

        public UInt32 ThisGameAddress         { get; private set; }
        public UInt32 UnitListAddress         { get; private set; }
        public UInt32 MoveSpeedAddress        { get; private set; }

        public UInt32 AttackAttributesOffset  { get; private set; }
        public UInt32 HeroAttributesOffset    { get; private set; }
        public UInt32 ItemsListOffset         { get; private set; }
        public UInt32 MoveSpeedOffset         { get; private set; }

        public uint _moduleAddress;

        // Get a context if the game is running and recognized.
        // Returns null if not running.
        // Raise exception if game version is not recognized.
        public static GameContext FindGameRunning(string processName, string moduleName)
        {
            Process[] processesByName = Process.GetProcessesByName(processName);
            if (processesByName.Length > 0)
            {
                return new GameContext(processesByName[0], moduleName);
            }
            return null;
        }

        public GameContext(Process gameProcess, string moduleName)
        {
            // Get PID
            GetProcessInfo(gameProcess);

            // Find module
            GetModuleInfo(gameProcess, moduleName);

            // Decide addresses according to version
            GetGameAddress();

            // Decide offsets according to version
            GetGameOffset();
        }

        private void GetProcessInfo(Process gameProcess)
        {
            try
            {
                this.ProcessId = gameProcess.Id;
            }
            catch
            {
                throw new InvalidOperationException("Failed to fetch process Id");
            }
        }

        private void GetModuleInfo(Process gameProcess, string moduleName)
        {
            // Find module
            WindowsApi.ProcessModule mainModule =
                new WindowsApi.ProcessModule(
                    ProcessId,
                    moduleName);

            // Check parameters
            string moduleFileName = mainModule.FileName;
            System.Diagnostics.FileVersionInfo moduleVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(moduleFileName);
            string fileVersion = moduleVersion.FileVersion;
            if (fileVersion == null)
                throw new InvalidOperationException("Bad file version");

            // Save info
            this.ProcessVersion = fileVersion.Replace(", ", ".");
            _moduleAddress = (uint)mainModule.BaseAddress;
        }

        private void GetGameAddress()
        {
            switch (ProcessVersion)
            {
                case "1.20.4.6074":
                    ThisGameAddress  = _moduleAddress + 0x87C744;
                    UnitListAddress  = _moduleAddress + 0x8722BC;
                    MoveSpeedAddress = _moduleAddress + 0x55BDF0;
                    break;
                case "1.21.0.6263":
                    ThisGameAddress  = _moduleAddress + 0x87D7BC;
                    UnitListAddress  = _moduleAddress + 0x873334;
                    MoveSpeedAddress = _moduleAddress + 0x55FE80;
                    break;
                case "1.21.1.6300":
                    ThisGameAddress  = _moduleAddress + 0x87D7BC;
                    UnitListAddress  = _moduleAddress + 0x873334;
                    MoveSpeedAddress = _moduleAddress + 0x55fEA0;
                    break;
                case "1.22.0.6328":
                    ThisGameAddress  = _moduleAddress + 0xAA4178;
                    UnitListAddress  = _moduleAddress + 0xAA2FFC;
                    MoveSpeedAddress = _moduleAddress + 0x201190;
                    break;
                case "1.23.0.6352":
                    ThisGameAddress  = _moduleAddress + 0xABCFC8;
                    UnitListAddress  = _moduleAddress + 0xABBE4C;
                    MoveSpeedAddress = _moduleAddress + 0x2026D0;
                    break;
                case "1.24.0.6372":
                case "1.24.1.6374":
                case "1.24.2.6378":
                case "1.24.3.6384":
                    ThisGameAddress  = _moduleAddress + 0xACE5E0;
                    UnitListAddress  = _moduleAddress + 0xACD44C;
                    MoveSpeedAddress = _moduleAddress + 0x202780;
                    break;
                case "1.24.4.6387":
                    ThisGameAddress  = _moduleAddress + 0xACE5E0;
                    UnitListAddress  = _moduleAddress + 0xACD44C;
                    MoveSpeedAddress = _moduleAddress + 0x2027E0;
                    break;
                case "1.25.1.6397":
                    ThisGameAddress  = _moduleAddress + 0xAB7788;
                    UnitListAddress  = _moduleAddress + 0xAB65F4;
                    MoveSpeedAddress = _moduleAddress + 0x201AA0;
                    break;
                case "1.26.0.6401":
                    ThisGameAddress  = _moduleAddress + 0xAB7788;
                    UnitListAddress  = _moduleAddress + 0xAB65F4;
                    MoveSpeedAddress = _moduleAddress + 0x201CD0;
                    break;
                case "1.27.0.52240":
                    ThisGameAddress  = _moduleAddress + 0xBE40A8;
                    UnitListAddress  = _moduleAddress + 0xBE4238;
                    MoveSpeedAddress = _moduleAddress + 0x5DF420;
                    break;
                case "1.28.0.7205":
                    ThisGameAddress = _moduleAddress + 0xD72F58;
                    UnitListAddress = _moduleAddress + 0xD730F0;
                    MoveSpeedAddress = _moduleAddress + 0x604470;
                    break;
                case "1.28.5.7680":
                    ThisGameAddress = _moduleAddress + 0xD30448;
                    UnitListAddress = _moduleAddress + 0xD305E0;
                    MoveSpeedAddress = _moduleAddress + 0x630C70;
                    break;
                default:
                    throw new UnkonwnGameVersionExpection(
                        this.ProcessId,
                        ProcessVersion);
            }
        }

        private void GetGameOffset()
        {
            switch (ProcessVersion)
            {
                case "1.20.4.6074":
                case "1.21.0.6263":
                case "1.21.1.6300":
                case "1.22.0.6328":
                case "1.23.0.6352":
                case "1.24.0.6372":
                case "1.24.1.6374":
                case "1.24.2.6378":
                    AttackAttributesOffset = 0x1E4;
                    HeroAttributesOffset   = 0x1EC;
                    ItemsListOffset        = 0x1F4;
                    MoveSpeedOffset        = 0x1D8;
                    break;
                case "1.24.3.6384":
                case "1.24.4.6387":
                case "1.25.1.6397":
                case "1.26.0.6401":
                case "1.27.0.52240":
                case "1.28.0.7205":
                case "1.28.5.7680":
                    AttackAttributesOffset = 0x1E8;
                    HeroAttributesOffset   = 0x1F0;
                    ItemsListOffset        = 0x1F8;
                    MoveSpeedOffset        = 0x1DC;
                    break;
                default:
                    System.Diagnostics.Debug.Assert(false, "Impossible to run to here");
                    break;
            }
        }
    }
}
