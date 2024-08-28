using System;
using System.Collections.Generic;

namespace War3Trainer
{
    #region Node type

    //////////////////////////////////////////////////////////////////////////
    // Function node
    enum TrainerNodeType//枚举，变量翻译为:训练结点类型
    {
        None,
        Root,

        Introduction,
        Cash,
        AllSelectedUnits,
        OneSelectedUnit,
        AttackAttributes,
        HeroAttributes,
        //UnitAbility,  // Not implemented yet未完成，
        //24.8.16,可以直接调用game.dll直接在屏幕中显示了
        AllItems,
        OneItem,
    }

    internal class NewChildrenEventArgs//储存游戏各种基地址，和在树形空间中的结点类型，父节点结点索引
        : EventArgs
    {
        public TrainerNodeType NodeType { get; set; }//这种格式的意思是自动生成一个get函数一个set函数默认public属性
        public int ParentNodeIndex { get; set; }

        public UInt32 ThisGameAddress { get; set; }   // [6FAA4178]
        public UInt32 ThisGameMemoryAddress { get; set; }   // [ThisGame + 0xC]
        public UInt32 ThisUnitAddress { get; set; }   // Unit ESI
        public UInt32 AttackAttributesAddress { get; set; }   // [ThisUnit + 1E4]
        public UInt32 HeroAttributesAddress { get; set; }   // [ThisUnit + 1EC]
        public UInt32 CurrentItemPackAddress { get; set; }   // [ThisUnit + 1F4]...

        public NewChildrenEventArgs()
        {
        }

        public NewChildrenEventArgs Clone()
        {
            NewChildrenEventArgs retObject = new NewChildrenEventArgs();

            retObject.NodeType = this.NodeType;
            retObject.ParentNodeIndex = this.ParentNodeIndex;
            retObject.ThisGameAddress = this.ThisGameAddress;
            retObject.ThisGameMemoryAddress = this.ThisGameMemoryAddress;
            retObject.ThisUnitAddress = this.ThisUnitAddress;
            retObject.AttackAttributesAddress = this.AttackAttributesAddress;
            retObject.HeroAttributesAddress = this.HeroAttributesAddress;
            retObject.CurrentItemPackAddress = this.CurrentItemPackAddress;

            return retObject;
        }//克隆一个新的结点并且继承该结构的数据并返回
    }

    internal interface ITrainerNode
    {
        TrainerNodeType NodeType { get; }
        string NodeTypeName { get; }
        int NodeIndex { get; }
        int ParentIndex { get; }
    }//在树形空间中需要在treeview上显示出来的字符串和结点的索引

    //////////////////////////////////////////////////////////////////////////
    // Address list node
    internal enum AddressListValueType//地址列表值类型
    {
        Integer,
        Float,
        Char4
    }

    internal class NewAddressListEventArgs//新的地址列表事件参数
        : EventArgs
    {
        public int ParentNodeIndex { get; private set; }
        public string Caption { get; private set; }

        public UInt32 Address { get; private set; }
        public AddressListValueType ValueType { get; private set; }
        public int ValueScale { get; private set; }

        public NewAddressListEventArgs(
            int parentNodeIndex,
            string caption,
            UInt32 address,
            AddressListValueType valueType)
            : this(parentNodeIndex, caption, address, valueType, 1)//构造函数，需要父节点索引，说明文字，地址，地址值类型
        {
        }

        public NewAddressListEventArgs(
            int parentNodeIndex,
            string caption,
            UInt32 address,
            AddressListValueType valueType,
            int valueScale)
        {
            this.ParentNodeIndex = parentNodeIndex;
            this.Caption = caption;

            this.Address = address;
            this.ValueType = valueType;
            this.ValueScale = valueScale;
        }//第二种构造函数，多了一个valuescale
    }

    internal interface IAddressNode
    {
        int ParentIndex { get; }

        string Caption { get; }
        UInt32 Address { get; }
        AddressListValueType ValueType { get; }
        int ValueScale { get; }
    }

    #endregion

    #region Basic nodes

    internal class TrainerNode
    {
        public event EventHandler<NewChildrenEventArgs> NewChildren;
        public event EventHandler<NewAddressListEventArgs> NewAddress;

        public virtual void CreateChildren()
        {
        }

        protected void CreateChild(
            TrainerNodeType childType,
            int parentIndex)
        {
            if (NewChildren != null)
            {
                NewChildrenEventArgs args = _newChildrenArgs.Clone();
                args.NodeType = childType;
                args.ParentNodeIndex = parentIndex;
                NewChildren(this, args);
            }
        }

        protected void CreateAddress(NewAddressListEventArgs addressListArgs)
        {
            if (NewAddress != null)
                NewAddress(this, addressListArgs);
        }

        protected int _nodeIndex;
        protected int _parentIndex;
        protected GameContext _gameContext;
        protected NewChildrenEventArgs _newChildrenArgs;

        public TrainerNode(
            int nodeIndex,
            GameContext gameContext,
            NewChildrenEventArgs args)
        {
            _nodeIndex = nodeIndex;
            _parentIndex = args.ParentNodeIndex;
            _gameContext = gameContext;
            _newChildrenArgs = args;
        }
    }

    class NodeAddressList
        : IAddressNode
    {
        protected NewAddressListEventArgs _AddresInfo;

        public NodeAddressList(NewAddressListEventArgs args)
        {
            _AddresInfo = args;
        }

        public int ParentIndex { get { return _AddresInfo.ParentNodeIndex; } }
        public string Caption { get { return _AddresInfo.Caption; } }
        public UInt32 Address { get { return _AddresInfo.Address; } }
        public AddressListValueType ValueType { get { return _AddresInfo.ValueType; } }
        public int ValueScale { get { return _AddresInfo.ValueScale; } }
    }

    #endregion

    #region Main collection

    //////////////////////////////////////////////////////////////////////////    
    // To build a node tree, and create objects using factory pattern
    class GameTrainer
    {
        private List<TrainerNode> _allTrainers = new List<TrainerNode>();
        private List<NodeAddressList> _allAdress = new List<NodeAddressList>();

        private GameContext _gameContext;

        #region Enumerator & Index

        public IEnumerable<ITrainerNode> GetFunctionList()
        {
            lock (_allTrainers)
            {
                foreach (var i in _allTrainers)
                    yield return i as ITrainerNode;
            }
        }

        public IEnumerable<IAddressNode> GetAddressList()
        {
            lock (_allAdress)
            {
                foreach (var i in _allAdress)
                    yield return i;
            }
        }

        #endregion

        // ctor()
        public GameTrainer(GameContext gameContext)
        {
            this._gameContext = gameContext;

            // Get trainers in 1st layer
            NewChildrenEventArgs args = new NewChildrenEventArgs();
            args.NodeType = TrainerNodeType.Root;
            NewChildrenEventReaction(this, args);

            // Get all other trainers
            int index = 0;
            int count = 1;
            while (index < count)
            {
                lock (_allTrainers)
                {
                    _allTrainers[index].CreateChildren();
                    index++;
                    count = _allTrainers.Count;
                }
            }
        }

        private void NewChildrenEventReaction(object sender, NewChildrenEventArgs e)
        {
            lock (_allTrainers)
            {
                TrainerNode newNode;
                int newNodeIndex = _allTrainers.Count;

                // Factory pattern                
                switch (e.NodeType)
                {
                    case TrainerNodeType.Root:
                        newNode = new RootNode(newNodeIndex, _gameContext, e);
                        break;
                    case TrainerNodeType.Introduction:
                        newNode = new IntroductionNode(newNodeIndex, _gameContext, e);
                        break;
                    case TrainerNodeType.Cash:
                        newNode = new CashNode(newNodeIndex, _gameContext, e);
                        break;
                    case TrainerNodeType.AllSelectedUnits:
                        newNode = new AllSelectedUnitsNode(newNodeIndex, _gameContext, e);
                        break;
                    case TrainerNodeType.OneSelectedUnit:
                        newNode = new OneSelectedUnitNode(newNodeIndex, _gameContext, e);
                        break;
                    case TrainerNodeType.AttackAttributes:
                        newNode = new AttackAttributesNode(newNodeIndex, _gameContext, e);
                        break;
                    case TrainerNodeType.HeroAttributes:
                        newNode = new HeroAttributesNode(newNodeIndex, _gameContext, e);
                        break;
                    // case TrainerNodeType.UnitAbility:
                    //    newNode = new UnitAbilityNode(newNodeIndex, _gameContext, e);
                    //    break;
                    case TrainerNodeType.AllItems:
                        newNode = new AllItemsNode(newNodeIndex, _gameContext, e);
                        break;
                    case TrainerNodeType.OneItem:
                        newNode = new OneItemNode(newNodeIndex, _gameContext, e);
                        break;
                    default:
                        throw new System.ArgumentException("e.NodeType");
                }
                newNode.NewChildren += NewChildrenEventReaction;
                newNode.NewAddress += NewAddressEventReaction;
                _allTrainers.Add(newNode);
            }
        }

        private void NewAddressEventReaction(object sender, NewAddressListEventArgs e)
        {
            lock (_allAdress)
            {
                _allAdress.Add(new NodeAddressList(e));
            }
        }
    }

    #endregion

    #region Every functions

    //////////////////////////////////////////////////////////////////////////
    // Concrete trainer
    class RootNode
        : TrainerNode, ITrainerNode
    {
        public TrainerNodeType NodeType { get { return TrainerNodeType.Root; } }
        public string NodeTypeName { get { return "所有功能"; } }

        public int NodeIndex { get { return _nodeIndex; } }
        public int ParentIndex { get { return _parentIndex; } }

        public RootNode(int nodeIndex, GameContext gameContext, NewChildrenEventArgs args)
            : base(nodeIndex, gameContext, args)
        {
        }

        public override void CreateChildren()
        {
            // This function will fill the following value(s):
            //      _newChildrenArgs.ThisGame
            //      _newChildrenArgs.ThisGameMemory
            War3Common.GetGameMemory(
                _gameContext,
                ref _newChildrenArgs);

            base.CreateChild(TrainerNodeType.Introduction, NodeIndex);
            base.CreateChild(TrainerNodeType.Cash, NodeIndex);
            base.CreateChild(TrainerNodeType.AllSelectedUnits, NodeIndex);
        }
    }

    class IntroductionNode
        : TrainerNode, ITrainerNode
    {
        public TrainerNodeType NodeType { get { return TrainerNodeType.Introduction; } }
        public string NodeTypeName { get { return "使用方法"; } }

        public int NodeIndex { get { return _nodeIndex; } }
        public int ParentIndex { get { return _parentIndex; } }

        public IntroductionNode(int nodeIndex, GameContext gameContext, NewChildrenEventArgs args)
            : base(nodeIndex, gameContext, args)
        {
        }

        public override void CreateChildren()
        {
        }
    }

    class CashNode
        : TrainerNode, ITrainerNode
    {
        public TrainerNodeType NodeType { get { return TrainerNodeType.Cash; } }
        public string NodeTypeName { get { return "游戏资源"; } }

        public int NodeIndex { get { return _nodeIndex; } }
        public int ParentIndex { get { return _parentIndex; } }

        public CashNode(int nodeIndex, GameContext gameContext, NewChildrenEventArgs args)
            : base(nodeIndex, gameContext, args)
        {
        }

        public override void CreateChildren()
        {
            UInt32 upperAddress;
            using (WindowsApi.ProcessMemory mem = new WindowsApi.ProcessMemory(_gameContext.ProcessId))
            {
                upperAddress = War3Common.ReadFromGameMemory(
                    mem, _gameContext, _newChildrenArgs,
                    1) & 0xFFFF0000;
            }
            if (upperAddress == 0)
                return;

            UInt32[] playerSourceBaseAddress = new UInt32[]
            {
                0,  // To skip index 0
                0x0190, 0x1410, 0x26A0, 0x3920, 0x4BB0,
                0x5E30, 0x70C0, 0x8350, 0x95D0, 0xA860,
                0xBAE0, 0xCD70
            };

            for (int i = 1; i <= 12; i++)
            {
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "P" + i.ToString() + " - 金",
                    unchecked(upperAddress + playerSourceBaseAddress[i] + 0),
                    AddressListValueType.Integer,
                    10));
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "P" + i.ToString() + " - 木",
                    unchecked(upperAddress + playerSourceBaseAddress[i] + 0x80),
                    AddressListValueType.Integer,
                    10));
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "P" + i.ToString() + " - 最大人口",
                    unchecked(upperAddress + playerSourceBaseAddress[i] + 0x180),
                    AddressListValueType.Integer));
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "P" + i.ToString() + " - 当前人口",
                    unchecked(upperAddress + playerSourceBaseAddress[i] + 0x200),
                    AddressListValueType.Integer));
            }
        }
    }

    class AllSelectedUnitsNode
        : TrainerNode, ITrainerNode
    {
        public TrainerNodeType NodeType { get { return TrainerNodeType.AllSelectedUnits; } }
        public string NodeTypeName { get { return "选中单位列表"; } }

        public int NodeIndex { get { return _nodeIndex; } }
        public int ParentIndex { get { return _parentIndex; } }

        public AllSelectedUnitsNode(int nodeIndex, GameContext gameContext, NewChildrenEventArgs args)
            : base(nodeIndex, gameContext, args)
        {
        }

        public override void CreateChildren()
        {
            using (WindowsApi.ProcessMemory mem = new WindowsApi.ProcessMemory(_gameContext.ProcessId))
            {
                // Get ESI of each selected unit
                UInt32 selectedUnitList = mem.ReadUInt32((IntPtr)_gameContext.UnitListAddress);
                UInt16 a2 = mem.ReadUInt16((IntPtr)unchecked(selectedUnitList + 0x28));
                UInt32 tmpAddress;
                tmpAddress = mem.ReadUInt32((IntPtr)unchecked(selectedUnitList + 0x58 + 4 * a2));
                tmpAddress = mem.ReadUInt32((IntPtr)unchecked(tmpAddress + 0x34));

                UInt32 listHead = mem.ReadUInt32((IntPtr)unchecked(tmpAddress + 0x1F0));
                // UInt32 listEnd = mem.ReadUInt32((IntPtr)unchecked(tmpAddress + 0x1F4));
                UInt32 listLength = mem.ReadUInt32((IntPtr)unchecked(tmpAddress + 0x1F8));

                UInt32 nextNode = listHead;
                // UInt32 nextNodeNot = ~listHead;
                for (int selectedUnitIndex = 0; selectedUnitIndex < listLength; selectedUnitIndex++)
                {
                    _newChildrenArgs.ThisUnitAddress = mem.ReadUInt32((IntPtr)unchecked(nextNode + 8));
                    Console.WriteLine($"{_newChildrenArgs.ThisUnitAddress:X}");
                    // nextNodeNot             = mem.ReadUInt32((IntPtr)unchecked(NextNode + 4));
                    nextNode = mem.ReadUInt32((IntPtr)unchecked(nextNode + 0));

                    base.CreateChild(TrainerNodeType.OneSelectedUnit, NodeIndex);//利用递归循环判断选定得的列表单位是否显示完毕
                }
            }
        }
    }

    class OneSelectedUnitNode
        : TrainerNode, ITrainerNode
    {
        public TrainerNodeType NodeType { get { return TrainerNodeType.OneSelectedUnit; } }
        public string NodeTypeName
        {
            get
            {
                // return "单位";
                return "0x"
                    + _newChildrenArgs.ThisUnitAddress.ToString("X")
                    + ": "
                    + GetUnitName();
            }
        }

        public int NodeIndex { get { return _nodeIndex; } }
        public int ParentIndex { get { return _parentIndex; } }

        public OneSelectedUnitNode(int nodeIndex, GameContext gameContext, NewChildrenEventArgs args)
            : base(nodeIndex, gameContext, args)
        {
        }

        public string GetUnitName()
        {
            using (WindowsApi.ProcessMemory mem = new WindowsApi.ProcessMemory(_gameContext.ProcessId))
            {
                return mem.ReadChar4((IntPtr)unchecked(_newChildrenArgs.ThisUnitAddress + 0x30));
            }
        }

        public override void CreateChildren()
        {
            using (WindowsApi.ProcessMemory mem = new WindowsApi.ProcessMemory(_gameContext.ProcessId))
            {
                _newChildrenArgs.AttackAttributesAddress = mem.ReadUInt32((IntPtr)unchecked(_newChildrenArgs.ThisUnitAddress + _gameContext.AttackAttributesOffset));
                _newChildrenArgs.HeroAttributesAddress = mem.ReadUInt32((IntPtr)unchecked(_newChildrenArgs.ThisUnitAddress + _gameContext.HeroAttributesOffset));

                if (_newChildrenArgs.AttackAttributesAddress > 0)
                {
                    base.CreateChild(TrainerNodeType.AttackAttributes, NodeIndex);
                    // base.CreateChild(TrainerNodeType.UnitAbility, NodeIndex);
                    base.CreateChild(TrainerNodeType.AllItems, NodeIndex);
                }

                if (_newChildrenArgs.HeroAttributesAddress > 0)
                {
                    base.CreateChild(TrainerNodeType.HeroAttributes, NodeIndex);
                }

                // Unit self propety(s)
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "单位名称",
                    unchecked(_newChildrenArgs.ThisUnitAddress + 0x30),
                    AddressListValueType.Char4));

                UInt32 tmpAddress1, tmpAddress2;
                Int32 tmpValue1, tmpValue2;
                tmpValue1 = mem.ReadInt32((IntPtr)unchecked(_newChildrenArgs.ThisUnitAddress + 0x98 + 0x8));
                tmpAddress1 = War3Common.ReadFromGameMemory(
                    mem, _gameContext, _newChildrenArgs,
                    tmpValue1);
                tmpAddress1 = unchecked(tmpAddress1 + 0x84);
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "HP - 目前",
                    unchecked(tmpAddress1 - 0xC),
                    AddressListValueType.Float));
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "HP - 最大",
                    tmpAddress1,
                    AddressListValueType.Float));
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "HP - 回复率",
                    unchecked(tmpAddress1 - 0x8),
                    AddressListValueType.Float));

                tmpValue1 = mem.ReadInt32((IntPtr)unchecked(_newChildrenArgs.ThisUnitAddress + 0x98 + 0x28));
                tmpAddress1 = War3Common.ReadFromGameMemory(
                    mem, _gameContext, _newChildrenArgs,
                    tmpValue1);
                tmpAddress1 = unchecked(tmpAddress1 + 0x84);
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "MP - 目前",
                    unchecked(tmpAddress1 - 0xC),
                    AddressListValueType.Float));
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "MP - 最大",
                    tmpAddress1,
                    AddressListValueType.Float));
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "MP - 回复率",
                    unchecked(tmpAddress1 - 0x8),
                    AddressListValueType.Float));

                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "盔甲 - 数量",
                    unchecked(_newChildrenArgs.ThisUnitAddress + 0xE0),
                    AddressListValueType.Float));
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "盔甲 - 种类",
                    unchecked(_newChildrenArgs.ThisUnitAddress + 0xE4),
                    AddressListValueType.Integer));

                // Move speed
                tmpAddress1 = unchecked(_newChildrenArgs.ThisUnitAddress + _gameContext.MoveSpeedOffset - 0x24);
                int i = 0;
                do
                {
                    Console.Write(i++ + ":" + tmpAddress1.ToString("X"));
                    tmpValue1 = mem.ReadInt32((IntPtr)unchecked(tmpAddress1 + 0x24));
                    Console.Write(tmpValue1.ToString("X") + " ");
                    tmpAddress1 = War3Common.ReadGameValue2(
                        mem, _gameContext, _newChildrenArgs,
                        tmpValue1);
                    Console.Write(tmpAddress1.ToString("X") + " ");
                    tmpAddress2 = mem.ReadUInt32((IntPtr)unchecked(tmpAddress1 + 0));
                    Console.Write(tmpAddress2.ToString("X") + " ");
                    tmpValue1 = mem.ReadInt32((IntPtr)unchecked(tmpAddress1 + 0x24));
                    tmpValue2 = mem.ReadInt32((IntPtr)unchecked(tmpAddress1 + 0x28));

                    // Note: If new game version released, set breakpoint here
                    //       and check tmpAddress2. Set this value as War3AddressMoveSpeed
                    tmpAddress2 = mem.ReadUInt32((IntPtr)unchecked(tmpAddress2 + 0x2D4));
                    Console.Write(tmpAddress2.ToString("X"));
                    if (_gameContext.MoveSpeedAddress == tmpAddress2)
                    {
                        CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                            "移动速度",
                            unchecked(tmpAddress1 + 0x70),  // +70 or +78 are both OK
                            AddressListValueType.Float));
                    }
                    Console.WriteLine("\n");
                } while (tmpValue1 > 0 && tmpValue2 > 0);

                // Coordinate
                tmpValue1 = mem.ReadInt32((IntPtr)unchecked(_newChildrenArgs.ThisUnitAddress + 0x164 + 8));
                tmpAddress1 = War3Common.ReadGameValue1(
                    mem, _gameContext, _newChildrenArgs,
                    tmpValue1);
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "坐标 - X",
                    tmpAddress1,
                    AddressListValueType.Float));
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "坐标 - Y",
                    unchecked(tmpAddress1 + 4),
                    AddressListValueType.Float));
            }
        }
    }

    class AttackAttributesNode
        : TrainerNode, ITrainerNode
    {
        public TrainerNodeType NodeType { get { return TrainerNodeType.AttackAttributes; } }
        public string NodeTypeName { get { return "战斗属性"; } }

        public int NodeIndex { get { return _nodeIndex; } }
        public int ParentIndex { get { return _parentIndex; } }

        public AttackAttributesNode(int nodeIndex, GameContext gameContext, NewChildrenEventArgs args)
            : base(nodeIndex, gameContext, args)
        {
        }

        public override void CreateChildren()
        {
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击频率比",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0x1B0),
                AddressListValueType.Float));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "主动攻击范围",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0x244),
                AddressListValueType.Float));

            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击1 - 倍乘",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0x88 + 0 * 4),
                AddressListValueType.Integer));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击1 - 骰子",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0x94 + 0 * 4),
                AddressListValueType.Integer));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击1 - 基础1",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0xA0 + 0 * 4),
                AddressListValueType.Integer));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击1 - 基础2",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0xAC + 0 * 4),
                AddressListValueType.Integer));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击1 - 丢失因子",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0xBC + 0 * 16),
                AddressListValueType.Float));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击1 - 攻击音效",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0xE8 + 0 * 4),
                AddressListValueType.Integer));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击1 - 种类",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0xF4 + 0 * 4),
                AddressListValueType.Integer));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击1 - 最大目标数",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0x100 + 0 * 4),
                AddressListValueType.Integer));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击1 - 间隔",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0x158 + 0 * 8),
                AddressListValueType.Float));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击1 - 首次延时",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0x16C + 0 * 16),
                AddressListValueType.Float));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击1 - 范围",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0x258 + 0 * 8),
                AddressListValueType.Float));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击1 - 范围缓冲",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0x26C + 0 * 8),
                AddressListValueType.Float));

            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击2 - 倍乘",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0x88 + 1 * 4),
                AddressListValueType.Integer));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击2 - 骰子",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0x94 + 1 * 4),
                AddressListValueType.Integer));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击2 - 基础1",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0xA0 + 1 * 4),
                AddressListValueType.Integer));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击2 - 基础2",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0xAC + 1 * 4),
                AddressListValueType.Integer));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击2 - 丢失因子",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0xBC + 1 * 16),
                AddressListValueType.Float));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击2 - 攻击音效",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0xE8 + 1 * 4),
                AddressListValueType.Integer));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击2 - 种类",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0xF4 + 1 * 4),
                AddressListValueType.Integer));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击2 - 最大目标数",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0x100 + 1 * 4),
                AddressListValueType.Integer));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击2 - 间隔",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0x158 + 1 * 8),
                AddressListValueType.Float));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击2 - 首次延时",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0x16C + 1 * 16),
                AddressListValueType.Float));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击2 - 范围",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0x258 + 1 * 8),
                AddressListValueType.Float));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "攻击2 - 范围缓冲",
                unchecked(_newChildrenArgs.AttackAttributesAddress + 0x26C + 1 * 8),
                AddressListValueType.Float));
        }
    }

    class HeroAttributesNode
        : TrainerNode, ITrainerNode
    {
        public TrainerNodeType NodeType { get { return TrainerNodeType.HeroAttributes; } }
        public string NodeTypeName { get { return "英雄属性"; } }

        public int NodeIndex { get { return _nodeIndex; } }
        public int ParentIndex { get { return _parentIndex; } }

        public HeroAttributesNode(int nodeIndex, GameContext gameContext, NewChildrenEventArgs args)
            : base(nodeIndex, gameContext, args)
        {
        }

        public override void CreateChildren()
        {
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "经验值",
                unchecked(_newChildrenArgs.HeroAttributesAddress + 0x8C),
                AddressListValueType.Integer));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "力量",
                unchecked(_newChildrenArgs.HeroAttributesAddress + 0x94),
                AddressListValueType.Integer));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "敏捷",
                unchecked(_newChildrenArgs.HeroAttributesAddress + 0xA8),
                AddressListValueType.Integer));

            using (WindowsApi.ProcessMemory mem = new WindowsApi.ProcessMemory(_gameContext.ProcessId))
            {
                UInt32 tmpAddress1;
                Int32 tmpValue1;
                tmpValue1 = mem.ReadInt32((IntPtr)unchecked(_newChildrenArgs.HeroAttributesAddress + 0x7C + 2 * 4));
                tmpAddress1 = War3Common.ReadGameValue1(
                    mem, _gameContext, _newChildrenArgs,
                    tmpValue1);
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "智力",
                    tmpAddress1,
                    AddressListValueType.Integer));
            }

            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "可用技能点",
                unchecked(_newChildrenArgs.HeroAttributesAddress + 0x90),
                AddressListValueType.Integer));

            for (UInt32 LearningAbilityIndex = 1; LearningAbilityIndex <= 5; LearningAbilityIndex++)
            {
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "学习技能" + LearningAbilityIndex.ToString() + " - 名称",
                    unchecked(_newChildrenArgs.HeroAttributesAddress + 0xF0 + LearningAbilityIndex * 4),
                    AddressListValueType.Char4));
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "学习技能" + LearningAbilityIndex.ToString() + " - 等级",
                    unchecked(_newChildrenArgs.HeroAttributesAddress + 0x108 + LearningAbilityIndex * 4),
                    AddressListValueType.Integer));
                CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                    "学习技能" + LearningAbilityIndex.ToString() + " - 要求",
                    unchecked(_newChildrenArgs.HeroAttributesAddress + 0x120 + LearningAbilityIndex * 4),
                    AddressListValueType.Integer));
            }
        }
    }
    /*
    class UnitAbilityNode
        : TrainerNode, ITrainerNode
    {
        public TrainerNodeType NodeType { get { return TrainerNodeType.UnitAbility; } }
        public string NodeTypeName { get { return "单位技能"; } }

        public int NodeIndex { get { return _nodeIndex; } }
        public int ParentIndex { get { return _parentIndex; } }
        
        public NodeUnitAbility(int nodeIndex, clsGameContext gameContext, NewChildrenEventArgs args)
            : base(nodeIndex, gameContext, args)
        {
        }
        
        public override void CreateChildren()
        {
        }
    }
    */
    class AllItemsNode
        : TrainerNode, ITrainerNode
    {
        public TrainerNodeType NodeType { get { return TrainerNodeType.AllItems; } }
        public string NodeTypeName { get { return "物品列表"; } }

        public int NodeIndex { get { return _nodeIndex; } }
        public int ParentIndex { get { return _parentIndex; } }

        public AllItemsNode(int nodeIndex, GameContext gameContext, NewChildrenEventArgs args)
            : base(nodeIndex, gameContext, args)
        {
        }

        public override void CreateChildren()
        {
            using (WindowsApi.ProcessMemory mem = new WindowsApi.ProcessMemory(_gameContext.ProcessId))
            {
                Int32 list = mem.ReadInt32((IntPtr)unchecked(_newChildrenArgs.ThisUnitAddress + _gameContext.ItemsListOffset));
                if (list != 0)
                {
                    for (Int32 itemIndex = 0; itemIndex < 6; itemIndex++)
                    {
                        UInt32 currentItem = 0;

                        // We assume ItemIndex never go out of bounds to the List
                        Int32 tmpValue1 = mem.ReadInt32((IntPtr)unchecked(list + 0xC * itemIndex + 0x70));
                        if (tmpValue1 > 0)
                        {
                            UInt32 RawItem = War3Common.ReadFromGameMemory(
                            mem, _gameContext, _newChildrenArgs,
                            tmpValue1);
                            if (RawItem != 0)
                            {
                                UInt32 tmpValue2 = mem.ReadUInt32((IntPtr)unchecked(RawItem + 0x20));
                                if (tmpValue2 == 0)
                                    currentItem = mem.ReadUInt32((IntPtr)unchecked(RawItem + 0x54));
                            }
                            if (currentItem != 0)
                            {
                                _newChildrenArgs.CurrentItemPackAddress = currentItem;
                                base.CreateChild(TrainerNodeType.OneItem, NodeIndex);
                            }
                        }
                    }   // foreach items
                }   // Item list exists
            }   // mem
        }   // CreateChildren()
    }

    class OneItemNode
        : TrainerNode, ITrainerNode
    {
        public TrainerNodeType NodeType { get { return TrainerNodeType.OneItem; } }
        public string NodeTypeName
        {
            get
            {
                // return "物品";
                return "0x"
                    + _newChildrenArgs.CurrentItemPackAddress.ToString("X")
                    + ": "
                    + GetItemName();
            }
        }

        public int NodeIndex { get { return _nodeIndex; } }
        public int ParentIndex { get { return _parentIndex; } }

        private string GetItemName()
        {
            using (WindowsApi.ProcessMemory mem = new WindowsApi.ProcessMemory(_gameContext.ProcessId))
            {
                return mem.ReadChar4((IntPtr)unchecked(_newChildrenArgs.CurrentItemPackAddress + 0x30));
            }
        }

        public OneItemNode(int nodeIndex, GameContext gameContext, NewChildrenEventArgs args)
            : base(nodeIndex, gameContext, args)
        {
        }

        public override void CreateChildren()
        {
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "物品名称",
                unchecked(_newChildrenArgs.CurrentItemPackAddress + 0x30),
                AddressListValueType.Char4));
            CreateAddress(new NewAddressListEventArgs(_nodeIndex,
                "使用次数",
                unchecked(_newChildrenArgs.CurrentItemPackAddress + 0x84),
                AddressListValueType.Integer));
        }
    }

    #endregion
}
