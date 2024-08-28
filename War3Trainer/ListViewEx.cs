using System;
using System.Windows.Forms;
using System.Security.Permissions;

namespace War3Trainer
{
    public partial class ListViewEx : ListView
    {
        private const int WM_HSCROLL = 0x0114;
        private const int WM_VSCROLL = 0x0115;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_MOUSEWHEEL = 0x020A;

        public event System.EventHandler Scrolling;
	
        public ListViewEx()
        {
            InitializeComponent();
        }

        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_VSCROLL ||
                m.Msg == WM_HSCROLL ||
                m.Msg == WM_KEYDOWN ||
                m.Msg == WM_MOUSEWHEEL)
            {
                if (Scrolling != null)
                    Scrolling(this, new EventArgs());
            }

            base.WndProc(ref m);
        }
    }
}
