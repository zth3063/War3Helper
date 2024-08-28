using System;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;
using System.IO;
//using System.Windows;

namespace War3Trainer
{
    public partial class MainForm : Form
    {
        private GameContext _currentGameContext;//确定游戏版本，各种基地址
        private GameTrainer _mainTrainer;
        WindowsApi.ProcessMemory ChangeMap;
        static int PageSwitch = 0;//切换页面显示
        int[] buttonStatus = new int[20];


        public MainForm()
        {
            InitializeComponent();
            SetRightGrid(RightFunction.Introduction);
        }

        private void FrmMain_Load(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Process.EnterDebugMode();
            }
            catch
            {
                ReportEnterDebugFailure();
                return;
            }

            FindGame();
        }

        /************************************************************************/
        /* Main functions                                                       */
        /************************************************************************/
        private bool FindGame()
        {
            bool isRecognized = false;//判断是否查找到对应版本游戏的标识符
            try
            {
                _currentGameContext = GameContext.FindGameRunning("war3", "game.dll");
                if (_currentGameContext != null)
                {
                    // Game online
                    ReportVersionOk(_currentGameContext.ProcessId, _currentGameContext.ProcessVersion);

                    // Get a new trainer
                    GetAllObject();

                    isRecognized = true;
                }
                else
                {
                    // Game offline
                    ReportNoGameFoundFailure();
                }
            }
            catch (UnkonwnGameVersionExpection ex)
            {
                // Unknown game version
                _currentGameContext = null;
                ReportVersionFailure(ex.ProcessId, ex.GameVersion);
            }
            catch (WindowsApi.BadProcessIdException ex)
            {
                this._currentGameContext = null;
                ReportProcessIdFailure(ex.ProcessId);
            }
            catch (Exception ex)
            {
                // Why here?
                _currentGameContext = null;
                ReportUnknownFailure(ex.Message);
            }

            // Enable buttons
            if (isRecognized)
            {
                viewFunctions.Enabled = true;
                viewData.Enabled = true;
                cmdGetAllObjects.Enabled = true;
                cmdModify.Enabled = true;
            }
            else
            {
                viewFunctions.Enabled = false;
                viewData.Enabled = false;
                cmdGetAllObjects.Enabled = false;
                cmdModify.Enabled = false;
            }
            return isRecognized;
        }

        private void GetAllObject()
        {
            // Check paramters
            if (_currentGameContext == null)
                return;

            // Get a new trainer
            _mainTrainer = new GameTrainer(_currentGameContext);
            ChangeMap = new WindowsApi.ProcessMemory(_currentGameContext.ProcessId);
            
            // Create function tree
            viewFunctions.Nodes.Clear();
            foreach (ITrainerNode currentFunction in _mainTrainer.GetFunctionList())
            {
                TreeNode[] parentNodes = viewFunctions.Nodes.Find(currentFunction.ParentIndex.ToString(), true);
                TreeNodeCollection parentTree;
                if (parentNodes.Length < 1)
                    parentTree = viewFunctions.Nodes;
                else
                    parentTree = parentNodes[0].Nodes;

                parentTree.Add(
                    currentFunction.NodeIndex.ToString(),
                    currentFunction.NodeTypeName)
                    .Tag = currentFunction;
            }
            viewFunctions.ExpandAll();

            // Switch to page 1
            TreeNode[] introductionNodes = viewFunctions.Nodes.Find("1", true);
            if (introductionNodes.Length > 0)
            {
                viewFunctions.SelectedNode = introductionNodes[0];
                SelectFunction(introductionNodes[0]);
            }
        }

        // Re-query specific tree-node by FunctionListNode
        private void RefreshSelectedObject(ITrainerNode currentFunction)
        {
            TreeNode[] currentNodes = viewFunctions.Nodes.Find(currentFunction.NodeIndex.ToString(), true);
            TreeNode currentTree;
            if (currentNodes.Length < 1)
                return;
            else
                currentTree = currentNodes[0];

            currentTree.Text = currentFunction.NodeTypeName;
        }

        private void SelectFunction(TreeNode functionNode)
        {
            if (functionNode == null)
                return;
            ITrainerNode node = functionNode.Tag as ITrainerNode;
            if (node == null)
                return;

            // Show introduction page
            if (node.NodeType == TrainerNodeType.Introduction)
            {
                SetRightGrid(RightFunction.Introduction);
            }
            else
            {
                // Fill address list
                FillAddressList(node.NodeIndex);

                // Show address list
                if (viewData.Items.Count > 0)
                    SetRightGrid(RightFunction.EditTable);
                else
                    SetRightGrid(RightFunction.Empty);
            }
        }

        private void FillAddressList(int functionNodeId)
        {
            // To set the right window
            viewData.Items.Clear();
            foreach (IAddressNode addressLine in _mainTrainer.GetAddressList())
            {
                if (addressLine.ParentIndex != functionNodeId)
                    continue;

                viewData.Items.Add(new ListViewItem(
                    new string[]
                    {
                        addressLine.Caption,    // Caption
                        "",                     // Original value
                        ""                      // Modified value
                    }));
                viewData.Items[viewData.Items.Count - 1].Tag = addressLine;
            }

            // To get memory content
            using (WindowsApi.ProcessMemory mem = new WindowsApi.ProcessMemory(_currentGameContext.ProcessId))
            {
                foreach (ListViewItem currentItem in viewData.Items)
                {
                    IAddressNode addressLine = currentItem.Tag as IAddressNode;
                    if (addressLine == null)
                        continue;

                    Object itemValue;
                    switch (addressLine.ValueType)
                    {
                        case AddressListValueType.Integer:
                            itemValue = mem.ReadInt32((IntPtr)addressLine.Address)
                                / addressLine.ValueScale;
                            break;
                        case AddressListValueType.Float:
                            itemValue = mem.ReadFloat((IntPtr)addressLine.Address)
                                / addressLine.ValueScale;
                            break;
                        case AddressListValueType.Char4:
                            itemValue = mem.ReadChar4((IntPtr)addressLine.Address);
                            break;
                        default:
                            itemValue = "";
                            break;
                    }
                    currentItem.SubItems[1].Text = itemValue.ToString();
                }
            }
        }

        // To apply the modifications
        private void ApplyModify()
        {
            using (WindowsApi.ProcessMemory mem = new WindowsApi.ProcessMemory(_currentGameContext.ProcessId))
            {
                foreach (ListViewItem currentItem in viewData.Items)
                {
                    string itemValueString = currentItem.SubItems[2].Text;
                    if (String.IsNullOrEmpty(itemValueString))
                    {
                        // Not modified
                        continue;
                    }

                    IAddressNode addressLine = currentItem.Tag as IAddressNode;
                    if (addressLine == null)
                        continue;

                    switch (addressLine.ValueType)
                    {
                        case AddressListValueType.Integer:
                            Int32 intValue;
                            if (!Int32.TryParse(itemValueString, out intValue))
                                intValue = 0;
                            intValue = unchecked(intValue * addressLine.ValueScale);
                            mem.WriteInt32((IntPtr)addressLine.Address, intValue);
                            break;
                        case AddressListValueType.Float:
                            float floatValue;
                            if (!float.TryParse(itemValueString, out floatValue))
                                floatValue = 0;
                            floatValue = unchecked(floatValue * addressLine.ValueScale);
                            mem.WriteFloat((IntPtr)addressLine.Address, floatValue);
                            break;
                        case AddressListValueType.Char4:
                            mem.WriteChar4((IntPtr)addressLine.Address, itemValueString);
                            break;
                    }
                    currentItem.SubItems[2].Text = "";
                }
            }
        }

        /************************************************************************/
        /* Exception UI                                                         */
        /************************************************************************/
        private void ReportEnterDebugFailure()
        {
            labGameScanState.Text = "请以管理员身份运行";
        }

        private void ReportNoGameFoundFailure()
        {
            labGameScanState.Text = "游戏未运行，运行游戏后单击“查找游戏”";
        }

        private void ReportUnknownFailure(string message)
        {
            labGameScanState.Text = "发生未知错误：" + message;
        }

        private void ReportProcessIdFailure(int processId)
        {
            labGameScanState.Text = "错误的进程ID："
                + processId.ToString();
        }

        private void ReportVersionFailure(int processId, string version)
        {
            labGameScanState.Text = "检测到游戏，但版本（"
                + version
                + "）不被支持";
        }

        private void ReportVersionOk(int processId, string version)
        {
            labGameScanState.Text = "检测到游戏（"
                + processId.ToString()
                + "），游戏版本："
                + version
                + "（支持）";
        }

        /************************************************************************/
        /* GUI                                                                  */
        /************************************************************************/
        private void MenuHelpAbout_Click(object sender, EventArgs e)
        {
            MessageBox.Show("游戏测试软件，方便修改属性" 
                + System.Environment.NewLine
                //+ Application.ProductVersion
                + "15.0.1.0:    24.8.27"
                + "增加单位无敌和秒杀单位功能,瞬移功能开发中,遇到了点困难"
                + "打算实现读取鼠标坐标,由绑定的按键触发,瞬移选中单位到地图坐标"
                + "不过得到鼠标坐标的功能我并不会"
                + "15.0.0.2:    24.8.23"
                + System.Environment.NewLine
                + "重新规划全图UI,取消自动启动,功能单独分离,增加配置文件"
                + System.Environment.NewLine
                + "15.0.0.2"
                + System.Environment.NewLine
                + "修正在1.27a版本下的回血回蓝偏移量错误问题"
                + System.Environment.NewLine
                + "15.0.0.1"
                + System.Environment.NewLine
                + "自动开启地图单位显示,技能查看"
                + System.Environment.NewLine
                + "",
                "提示",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void MenuFileExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void cmdGetAllObjects_Click(object sender, EventArgs e)
        {
            try
            {
                GetAllObject();
            }
            catch (WindowsApi.BadProcessIdException ex)
            {
                ReportProcessIdFailure(ex.ProcessId);
            }
        }

        void ButtonBackSwitch(String str)
        {
            this.BasicFunction.Enabled = true;
            this.OtherFunction.Enabled = true;
            this.BasicFunction.BackColor = System.Drawing.SystemColors.Control;
            this.OtherFunction.BackColor = System.Drawing.SystemColors.Control;
            switch (str)
            {
                case "数据修改":
                    this.BasicFunction.BackColor = System.Drawing.SystemColors.ControlDark;
                    break;
                case "其他功能":
                    this.OtherFunction.BackColor = System.Drawing.SystemColors.ControlDark;
                    break;
                default:
                    break;
            }
        }

        private void cmdScanGame_Click(object sender, EventArgs e)
        {
            PanelHide();
            this.splitMain.Panel1.Visible = true;
            this.splitMain.Panel2.Visible = true;
            if (FindGame())
            {
                this.cmdScanGame.Text = "重新查找";
                ButtonBackSwitch("数据修改");
            }
        }

        private void cmdModify_Click(object sender, EventArgs e)//通过这个点击找到游戏中选定的对象
        {
            try
            {
                ApplyModify();

                // Refresh left
                TreeNode selectedNode = viewFunctions.SelectedNode;
                if (selectedNode == null)
                    return;

                ITrainerNode functionNode = selectedNode.Tag as ITrainerNode;
                if (functionNode != null)
                    RefreshSelectedObject(functionNode);

                // Refresh right
                SelectFunction(selectedNode);
            }
            catch (WindowsApi.BadProcessIdException ex)
            {
                ReportProcessIdFailure(ex.ProcessId);
            }
        }

        private void viewFunctions_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            // Check whether modification is not saved
            bool isSaved = true;
            foreach (ListViewItem currentItem in viewData.Items)
            {
                if (!String.IsNullOrEmpty(currentItem.SubItems[2].Text))
                {
                    isSaved = false;
                    break;
                }
            }

            // Save all if not saved
            if (!isSaved)
            {
                cmdModify_Click(this, null);
            }

            // Select another function
            try
            {
                SelectFunction(e.Node);
            }
            catch (WindowsApi.BadProcessIdException ex)
            {
                ReportProcessIdFailure(ex.ProcessId);
            }
        }

        private enum RightFunction
        {
            Empty,
            Introduction,
            EditTable,
        }

        private void SetRightGrid(RightFunction function)
        {
            this.splitMain.Panel2.SuspendLayout();
            this.viewData.SuspendLayout();

            txtIntroduction.Visible = function == RightFunction.Introduction;
            viewData.Visible = function == RightFunction.EditTable;
            lblEmpty.Visible = function == RightFunction.Empty;

            txtIntroduction.Dock = DockStyle.Fill;
            viewData.Dock = DockStyle.Fill;
            lblEmpty.Location = new Point(0, 0);

            this.viewData.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            this.splitMain.Panel2.PerformLayout();
        }

        //////////////////////////////////////////////////////////////////////////       
        // Make the ListView editable
        private void ReplaceInputTextbox()
        {
            if (viewData.SelectedItems.Count < 1)
                return;
            ListViewItem currentItem = viewData.SelectedItems[0];

            txtInput.Location = new Point(
                viewData.Columns[0].Width + viewData.Columns[1].Width,
                currentItem.Position.Y - 2);
            txtInput.Width = viewData.Columns[2].Width;
        }

        private void viewData_KeyPress(object sender, KeyPressEventArgs e)
        {
            switch ((Keys)e.KeyChar)
            {
                case Keys.Enter:
                    viewData_MouseUp(sender, null);
                    e.Handled = true;
                    break;
            }
        }

        private void viewData_MouseUp(object sender, MouseEventArgs e)
        {
            // Get item
            if (viewData.SelectedItems.Count < 1)
                return;
            ListViewItem currentItem = viewData.SelectedItems[0];

            // Determine the content of edit box
            ReplaceInputTextbox();

            txtInput.Tag = currentItem;

            int textToEdit;
            if (String.IsNullOrEmpty(currentItem.SubItems[2].Text))
                textToEdit = 1;
            else
                textToEdit = 2;
            txtInput.Text = currentItem.SubItems[textToEdit].Text;

            // Enable editing
            txtInput.Visible = true;
            txtInput.Focus();
            txtInput.Select(0, 0);  // Cancel select all
        }

        private void viewData_ColumnWidthChanging(object sender, ColumnWidthChangingEventArgs e)
        {
            ReplaceInputTextbox();
        }

        private void viewData_Scrolling(object sender, EventArgs e)
        {
            viewData.Focus();
        }

        private void txtInput_Leave(object sender, EventArgs e)
        {
            txtInput.Visible = false;
            ListViewItem currentItem = txtInput.Tag as ListViewItem;
            if (currentItem == null)
                return;

            if (currentItem.SubItems[1].Text != txtInput.Text)
                currentItem.SubItems[2].Text = txtInput.Text;
            else
                currentItem.SubItems[2].Text = "";
        }

        private void txtInput_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    CommitEditAndMoveNext(sender, 1);
                    e.Handled = true;
                    break;
                case Keys.Up:
                    CommitEditAndMoveNext(sender, -1);
                    e.Handled = true;
                    break;
                case Keys.Down:
                    CommitEditAndMoveNext(sender, 1);
                    e.Handled = true;
                    break;
                case Keys.Escape:
                    DiscardEdit(sender);
                    e.Handled = true;
                    break;
            }
        }

        private void DiscardEdit(object editBox)
        {
            // Roll back content of the edit box
            viewData_MouseUp(editBox, null);

            // Hide edit box
            txtInput_Leave(editBox, null);

            // Restore focus
            viewData.Focus();
        }

        private void CommitEditAndMoveNext(object editBox, int delta)
        {
            // Commit
            txtInput_Leave(editBox, null);

            // Move to another line
            viewData.Focus();
            if (viewData.SelectedItems.Count > 0)
            {
                int nextIndex = viewData.SelectedItems[0].Index + delta;
                if (nextIndex < viewData.Items.Count &&
                    nextIndex >= 0)
                {
                    viewData.Items[nextIndex].Selected = true;
                    viewData.Items[nextIndex].Focused = true;
                    viewData.Items[nextIndex].EnsureVisible();
                }
                viewData_MouseUp(editBox, null);
            }
        }

        /************************************************************************/
        /* Debug                                                                */
        /************************************************************************/
        private void menuDebug1_Click(object sender, EventArgs e)
        {
            string strIndex = Microsoft.VisualBasic.Interaction.InputBox(
                "nIndex = 0x?",
                "War3Common.ReadFromGameMemory(nIndex)",
                "0", -1, -1);
            if (String.IsNullOrEmpty(strIndex))
                return;

            Int32 nIndex;
            if (!Int32.TryParse(
                strIndex,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.NumberFormatInfo.InvariantInfo,
                out nIndex))
            {
                nIndex = 0;
                
            }

            try
            {
                UInt32 result = 0;
                using (WindowsApi.ProcessMemory mem = new WindowsApi.ProcessMemory(_currentGameContext.ProcessId))
                {
                    NewChildrenEventArgs args = new NewChildrenEventArgs();
                    War3Common.GetGameMemory(
                        _currentGameContext, ref args);
                    result = War3Common.ReadFromGameMemory(
                        mem, _currentGameContext, args,
                        nIndex);
                }
                MessageBox.Show(
                    "0x" + result.ToString("X"),
                    "War3Common.ReadFromGameMemory(0x" + strIndex + ")");
            }
            catch (WindowsApi.BadProcessIdException ex)
            {
                ReportProcessIdFailure(ex.ProcessId);
            }
        }

        private void txtIntroduction_TextChanged(object sender, EventArgs e)
        {

        }
        //隐藏所有Panel
        void PanelHide()
        {
            this.panel1.Visible = false;
            this.splitMain.Panel2.Visible = false;
            this.splitMain.Panel1.Visible = false;
        }
        private void OtherFunction_Click(object sender, EventArgs e)
        {
            //隐藏所有页面,再展示要显示的页面
            PanelHide();
            PageSwitch = 1;
            this.panel1.Visible = true;
            ButtonBackSwitch("其他功能");
        }
        private void toolStripMain_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            PanelHide();
            this.splitMain.Panel2.Visible = true;
            this.splitMain.Panel1.Visible = true;
        }

        private void splitMain_Panel2_Paint(object sender, PaintEventArgs e)
        {

        }

        private void viewFunctions_AfterSelect(object sender, TreeViewEventArgs e)
        {

        }

        private void splitMain_BackColorChanged(object sender, EventArgs e)
        {

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            buttonStatus[this.checkBox100.TabIndex] = checkBox100.Checked ? 1 : 0;
            int bytesWriten;
            if (checkBox100.Checked)
            {

                //单位显示技能
                UInt32 minMapUnitSkillAddr = 0x392818;
                byte[] minMapUnitSkill = { 0x90, 0x90 };
                ChangeMap.WriteBytes(minMapUnitSkill, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitSkillAddr), sizeof(byte) * 2, out bytesWriten);
            }
            else
            {
                UInt32 minMapUnitSkillAddr = 0x392818;
                byte[] minMapUnitSkill = { 0x74, 0x08 };
                ChangeMap.WriteBytes(minMapUnitSkill, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitSkillAddr), sizeof(byte) * 2, out bytesWriten);
            }
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            buttonStatus[this.checkBox101.TabIndex] = checkBox101.Checked ? 1 : 0;
            int bytesWriten;
            /*老方法全图,关闭全图之后小地图依旧显示建筑
            //大地图显示敌对单位
            if (checkBox2.Checked)
            {
                UInt32 minMapUnitAddr = 0x1BFEE5;
                byte[] minMapUnit = { 0x74, 0x34 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
            else
            {
                UInt32 minMapUnitAddr = 0x1BFEE5;
                byte[] minMapUnit = { 0x75, 0x34 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);
               
            }
            */
            //尝试新方法,可能会异步?
            if (checkBox101.Checked)
            {
                UInt32 minMapUnitAddr = 0x66E71E;
                byte[] minMapUnit = { 0xEB, 0x31 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
            else
            {
                UInt32 minMapUnitAddr = 0x66E71E;
                byte[] minMapUnit = { 0x75, 0x31 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
        }

        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            buttonStatus[this.checkBox104.TabIndex] = checkBox104.Checked ? 1 : 0;
            int bytesWriten;
            //大地图显隐
            if (checkBox104.Checked)
            {
                UInt32 minMapUnitAddr = 0x370AD3;
                byte[] minMapUnit = { 0x75, 0x26 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);

                //大地图帧率
                UInt32 minMapUnitAddr0 = 0x370A07;
                byte[] minMapUnit0 = { 0x75, 0x0C };
                ChangeMap.WriteBytes(minMapUnit0, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr0), sizeof(byte) * 2, out bytesWriten);
                
                UInt32 minMapUnitAddr1 = 0x370A0F;
                byte[] minMapUnit1 = { 0x0F, 0x84 };
                ChangeMap.WriteBytes(minMapUnit1, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr1), sizeof(byte) * 2, out bytesWriten);
            }
            else
            {
                UInt32 minMapUnitAddr = 0x370AD3;
                byte[] minMapUnit = { 0x74, 0x26 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);

                UInt32 minMapUnitAddr0 = 0x370A07;
                byte[] minMapUnit0 = { 0x75, 0x0C };
                ChangeMap.WriteBytes(minMapUnit0, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr0), sizeof(byte) * 2, out bytesWriten);

                UInt32 minMapUnitAddr1 = 0x370A0F;
                byte[] minMapUnit1 = { 0x0F, 0x85 };
                ChangeMap.WriteBytes(minMapUnit1, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr1), sizeof(byte) * 2, out bytesWriten);
            }
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void labGameScanState_Click(object sender, EventArgs e)
        {

        }

        private void BasicFunction_Click(object sender, EventArgs e)
        {
            ButtonBackSwitch("数据修改");
        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            buttonStatus[this.checkBox102.TabIndex] = checkBox102.Checked ? 1 : 0;
            int bytesWriten;
            //弹幕可见
            if (checkBox102.Checked)
            {
                UInt32 minMapUnitAddr = 0x36F670;
                byte[] minMapUnit = { 0x90, 0x90 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);

            }
            else
            {
                UInt32 minMapUnitAddr = 0x36F670;
                byte[] minMapUnit = { 0x74, 0x23 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
        }

        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            buttonStatus[this.checkBox103.TabIndex] = checkBox103.Checked ? 1 : 0;
            int bytesWriten;
            //单位可选
            if (checkBox103.Checked)
            {
                UInt32 minMapUnitAddr = 0x6516A3;
                byte[] minMapUnit = { 0xEB, 0x16 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);

                //单位可点击
                UInt32 minMapUnitAddr4 = 0x1BFF0D;
                byte[] minMapUnit4 = { 0x74, 0x04 };
                ChangeMap.WriteBytes(minMapUnit4, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr4), sizeof(byte) * 2, out bytesWriten);

                UInt32 minMapUnitAddr5 = 0x1BFF11;
                byte[] minMapUnit5 = { 0xEB, 0x10 };
                ChangeMap.WriteBytes(minMapUnit5, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr5), sizeof(byte) * 2, out bytesWriten);

            }
            else
            {
                UInt32 minMapUnitAddr = 0x6516A3;
                byte[] minMapUnit = { 0x75, 0x16 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);

                UInt32 minMapUnitAddr4 = 0x1BFF0D;
                byte[] minMapUnit4 = { 0x75, 0x04 };
                ChangeMap.WriteBytes(minMapUnit4, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr4), sizeof(byte) * 2, out bytesWriten);

                UInt32 minMapUnitAddr5 = 0x1BFF11;
                byte[] minMapUnit5 = { 0x74, 0x10 };
                ChangeMap.WriteBytes(minMapUnit5, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr5), sizeof(byte) * 2, out bytesWriten);

            }
        }

        private void checkBox13_CheckedChanged(object sender, EventArgs e)
        {
            buttonStatus[this.checkBox105.TabIndex] = checkBox105.Checked ? 1 : 0;
            int bytesWriten;
            //移除黑色迷雾
            if (checkBox105.Checked)
            {
                UInt32 minMapUnitAddr = 0x3B947C;
                byte[] minMapUnit = { 0x90, 0x90 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
            else
            {
                UInt32 minMapUnitAddr = 0x3B947C;
                byte[] minMapUnit = { 0x74, 0x04 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
        }

        private void checkBox12_CheckedChanged(object sender, EventArgs e)
        {
            buttonStatus[this.checkBox106.TabIndex] = checkBox106.Checked ? 1 : 0;
            int bytesWriten;
            //显示镜像
            if (checkBox106.Checked)
            {
                UInt32 minMapUnitAddr1 = 0x66E459;
                byte[] minMapUnit1 = { 0xC3, 0xCC };
                ChangeMap.WriteBytes(minMapUnit1, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr1), sizeof(byte) * 2, out bytesWriten);

                UInt32 minMapUnitAddr = 0x66E457;
                byte[] minMapUnit = { 0x66, 0x40 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
            else
            {
                UInt32 minMapUnitAddr1 = 0x66E459;
                byte[] minMapUnit1 = { 0xCC, 0xCC };
                ChangeMap.WriteBytes(minMapUnit1, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr1), sizeof(byte) * 2, out bytesWriten);

                UInt32 minMapUnitAddr = 0x66E457;
                byte[] minMapUnit = { 0xC3, 0xCC };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
        }

        private void checkBox11_CheckedChanged(object sender, EventArgs e)
        {
            buttonStatus[this.checkBox107.TabIndex] = checkBox107.Checked ? 1 : 0;
            int bytesWriten;
            //显示资源
            if (checkBox107.Checked)
            {
                UInt32 minMapUnitAddr = 0x3AAA23;
                byte[] minMapUnit = { 0x90, 0x90 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
            else
            {
                UInt32 minMapUnitAddr = 0x3AAA23;
                byte[] minMapUnit = { 0xEB, 0x0A };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
        }

        private void checkBox10_CheckedChanged(object sender, EventArgs e)
        {
            buttonStatus[this.checkBox108.TabIndex] = checkBox108.Checked ? 1 : 0;
            int bytesWriten;
            //物品可见
            if (checkBox108.Checked)
            {
                UInt32 minMapUnitAddr = 0x1C0053;
                byte[] minMapUnit = { 0x74, 0x34 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
            else
            {
                UInt32 minMapUnitAddr = 0x1C0053;
                byte[] minMapUnit = { 0x75, 0x34 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
        }

        private void checkBox9_CheckedChanged(object sender, EventArgs e)
        {
            buttonStatus[this.checkBox109.TabIndex] = checkBox109.Checked ? 1 : 0;
            int bytesWriten;
            //物品可选
            if (checkBox109.Checked)
            {
                UInt32 minMapUnitAddr = 0x1C0144;
                byte[] minMapUnit = { 0x74, 0x17 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
            else
            {
                UInt32 minMapUnitAddr = 0x1C0144;
                byte[] minMapUnit = { 0x75, 0x17 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
        }

        private void checkBox8_CheckedChanged(object sender, EventArgs e)
        {
            buttonStatus[this.checkBox110.TabIndex] = checkBox110.Checked ? 1 : 0;
            int bytesWriten;
            /*
            //小地图显示中立单位
            if (checkBox8.Checked)
            {
                UInt32 minMapAIUnitAddr = 0x3BD7C2;
                byte[] minMapAIUnit = { 0xEB, 0x2E };
                ChangeMap.WriteBytes(minMapAIUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapAIUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
            else
            {
                UInt32 minMapAIUnitAddr = 0x3BD7C2;
                byte[] minMapAIUnit = { 0x74, 0x2E };
                ChangeMap.WriteBytes(minMapAIUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapAIUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
            */
            //移除战争迷雾
            if (checkBox110.Checked)
            {
                UInt32 minMapAIUnitAddr = 0x740420;
                byte[] minMapAIUnit = { 0x90, 0x90 };
                ChangeMap.WriteBytes(minMapAIUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapAIUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
            else
            {
                UInt32 minMapAIUnitAddr = 0x740420;
                byte[] minMapAIUnit = { 0x74, 0x04 };
                ChangeMap.WriteBytes(minMapAIUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapAIUnitAddr), sizeof(byte) * 2, out bytesWriten);
            }
        }

        private void checkBox14_CheckedChanged(object sender, EventArgs e)
        {
            buttonStatus[this.checkBox111.TabIndex] = checkBox111.Checked ? 1 : 0;
            int bytesWriten;
            //小地图ping
            if (checkBox111.Checked)
            {
                UInt32 minMapUnitAddr = 0x251275;
                byte[] minMapUnit = { 0x0F, 0x85 };
                UInt32 minMapUnitAddr2 = 0x251288;
                byte[] minMapUnit2 = { 0x0F, 0x85 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);
                ChangeMap.WriteBytes(minMapUnit2, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr2), sizeof(byte) * 2, out bytesWriten);
            }
            else
            {
                UInt32 minMapUnitAddr = 0x251275;
                byte[] minMapUnit = { 0x0F, 0x84 };
                UInt32 minMapUnitAddr2 = 0x251288;
                byte[] minMapUnit2 = { 0x0F, 0x84 };
                ChangeMap.WriteBytes(minMapUnit, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr), sizeof(byte) * 2, out bytesWriten);
                ChangeMap.WriteBytes(minMapUnit2, (IntPtr)(_currentGameContext._moduleAddress + minMapUnitAddr2), sizeof(byte) * 2, out bytesWriten);
            }
        }

        bool SetMapHackConfig()
        {
            bool a = false;
            XmlDocument xmlDoc = new XmlDocument();
            string configFilePath = "UserConfig.xml";
            XmlNode root = null;
            if (File.Exists(configFilePath))
            {
                a = true;
                xmlDoc.Load(configFilePath);
                root = xmlDoc.SelectSingleNode("/configuration");
            }
            for (int i = 0; i < 12; i++)
            {
                string settingNode = "MapNode" + i;
                //xmlDoc.SelectSingleNode("//" + settingNode).InnerText = (buttonStatus[i].ToString());
                XmlNode xmlnode =  xmlDoc.SelectSingleNode("//" + settingNode);
                if(xmlnode == null)
                {
                    xmlnode = xmlDoc.CreateElement("MapNode" + i);
                    xmlnode.InnerText = buttonStatus[i].ToString();
                    root.AppendChild(xmlnode);
                    continue;
                }
                xmlnode.InnerText = (buttonStatus[i].ToString());
                root.AppendChild(xmlnode);
            }
            xmlDoc.Save(configFilePath);
            return a;
        }
        int[] GetMapHackConfig()
        {
            XmlDocument xmlDoc = new XmlDocument();
            string configFilePath = "UserConfig.xml";
            if (File.Exists(configFilePath))
            {  
                xmlDoc.Load(configFilePath);
            }
            int []a = new int[20];
            for(int i = 0; i < 12; i++)
            {
                string settingNode = "MapNode" + i;
                int settingValue = int.Parse(xmlDoc.SelectSingleNode("//" + settingNode).InnerText);
                a[i] = settingValue;
            }
            return a; 
        }

        private void checkBox15_CheckedChanged(object sender, EventArgs e)
        {
            //这里加了个配置文件,进行一键配置
            if (checkBox15.Checked)
            {
                buttonStatus = GetMapHackConfig();
                for (int i = 0; i < 12; i++)
                {
                    foreach (Control control in this.panel1.Controls)
                    {
                        if (control is CheckBox checkBox)
                        {
                            
                            int num = 100 + i;
                            string name = "checkBox" + num.ToString();
                            if (checkBox.Name.Equals(name))
                            {
                                if (buttonStatus[i]==0)
                                {
                                    checkBox.Checked = false;
                                }
                                else
                                {
                                    checkBox.Checked = true;
                                }
                                
                            }
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < 12; i++)
                {
                    if(buttonStatus[i] == 0)
                    {
                        continue;
                    }
                    int num = 100 + i;
                    string name = "checkBox" + num.ToString();
                    foreach (Control control in this.panel1.Controls)
                    {
                        if (control is CheckBox checkBox)
                        {
                            if (checkBox.Name.Equals(name))
                            {
                                if(checkBox.Checked == true)
                                {
                                    checkBox.Checked = false;
                                }
                            }
                        }
                    }
                }
                for (int i = 0; i < 12; i++)
                {
                    buttonStatus[i] = 0;
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SetMapHackConfig();
        }

        private void checkBox1_CheckedChanged_1(object sender, EventArgs e)
        {

        }

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void label6_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void label7_Click(object sender, EventArgs e)
        {

        }

        private void button2_Click(object sender, EventArgs e)
        {
            if(button2.Text == "尝试注入" || button2.Text == "重试")
            {
                if (!InjectFunction.injectDLL())
                {
                    button2.Text = "重试";
                    MessageBox.Show("游戏测试软件，方便修改属性", "错误", MessageBoxButtons.OK);
                }
                else
                {
                    button2.Text = "卸载模块";
                }
            }
            else
            {
                InjectFunction.removeDLL();
                button2.Text = "尝试注入";
            }
            
        }

        private void button101_Click(object sender, EventArgs e)
        {
            InjectFunction.SendMsg(new IntPtr(1), new IntPtr(1));
        }

        private void button111_Click(object sender, EventArgs e)
        {
            InjectFunction.SendMsg(new IntPtr(2), new IntPtr(0));
        }

        private void button121_Click(object sender, EventArgs e)
        {
            InjectFunction.SendMsg(new IntPtr(1), new IntPtr(0));
        }

        private void button131_Click(object sender, EventArgs e)
        {
            InjectFunction.SendMsg(new IntPtr(3), new IntPtr(0));
        }
    }
}
