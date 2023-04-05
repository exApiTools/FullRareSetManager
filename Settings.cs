using System.Collections.Generic;
using System.Windows.Forms;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Nodes;

namespace FullRareSetManager
{
    public class FullRareSetManagerSettings : ISettings
    {
        public List<int> AllowedStashTabs = new List<int>();

        [Menu("", "Registering after using DropToInventoryKey to NPC trade inventory")]
        public TextNode SetsAmountStatisticsText { get; set; } = "Total sets sold to vendor: N/A";
        public int SetsAmountStatistics { get; set; }
        [Menu("Position X")]
        public RangeNode<float> PositionX { get; set; } = new RangeNode<float>(0.0f, 0.0f, 2000.0f);

        [Menu("Width Multiplier")]
        public RangeNode<float> WidthMultiplier { get; set; } = new RangeNode<float>(1f, 0, 3);
        [Menu("Position Y")]
        public RangeNode<float> PositionY { get; set; } = new RangeNode<float>(365.0f, 0.0f, 2000.0f);

        [Menu("Height Multiplier")]
        public RangeNode<float> HeightMultiplier { get; set; } = new RangeNode<float>(1f, 0, 3);
        [Menu("Allow Identified Items")]
        public ToggleNode AllowIdentified { get; set; } = new ToggleNode(false);

        [Menu("Show only with inventory")]
        public ToggleNode ShowOnlyWithInventory { get; set; } = new ToggleNode(false);

        [Menu("Hide when left panel opened")]
        public ToggleNode HideWhenLeftPanelOpened { get; set; } = new ToggleNode(false);

        [Menu("Show Regal sets")]
        public ToggleNode ShowRegalSets { get; set; } = new ToggleNode(false);

        [Menu("Optimize for Chaos sets","will pickup at least 1 low level item and others will be high level")]
        public ToggleNode OptimizeChaosSets { get; set; } = new ToggleNode(true);

        [Menu("Priority",
            "Weapon prepare priority in list of set items. If you have 1-handed and 2-handed weapons- it will consider this option.")]
        public ListNode WeaponTypePriority { get; set; } = new ListNode {Value = "Two handed"};

        [Menu("Only small weapons", "Small means 1x3 slots weapon.")]
        public ToggleNode SmallWeaponOnly { get; set; } = new ToggleNode(false);
        [Menu("Max Collecting Sets (0 disable)",
            "Amount of sets you going to collect. It will display lower pick priority if amount of item are more than this value.")]
        public RangeNode<int> MaxSets { get; set; } = new RangeNode<int>(0, 0, 30);

        [Menu("Drop To Invent Key", "It will also drop items to NPC trade window inventory")]
        public HotkeyNode DropToInventoryKey { get; set; } = Keys.F5;

        [Menu("Extra Click Delay")]
        public RangeNode<int> ExtraDelay { get; set; } = new RangeNode<int>(50, 0, 2000);

        [Menu("Items Lables Borders", 0)]
        public ToggleNode EnableBorders { get; set; } = new ToggleNode(false);

        [Menu("Inventory Borders")]
        public ToggleNode InventBorders { get; set; } = new ToggleNode(false);

        [Menu("Borders Width", 1, 0)]
        public RangeNode<int> BorderWidth { get; set; } = new RangeNode<int>(5, 1, 15);

        [Menu("Borders Oversize", 2, 0)]
        public RangeNode<int> BorderOversize { get; set; } = new RangeNode<int>(5, 0, 15);

        [Menu("Resize Borders accord. to Pick Priority",
            "That will change borders width, oversize depending on pick priority.", 3, 0)]
        public ToggleNode BorderAutoResize { get; set; } = new ToggleNode(false);

        [Menu("Text Size", 4, 0)]
        public RangeNode<int> TextSize { get; set; } = new RangeNode<int>(20, 0, 30);

        [Menu("Text Offset X", 5, 0)]
        public RangeNode<float> TextOffsetX { get; set; } = new RangeNode<float>(0, -50, 12);

        [Menu("Text Offset Y", 6, 0)]
        public RangeNode<float> TextOffsetY { get; set; } = new RangeNode<float>(-20, -50, 12);

        [Menu("Don't Higlight One Handed", 7, 0)]
        public ToggleNode IgnoreOneHanded { get; set; } = new ToggleNode(false);

        [Menu("Don't Higlight 100% except Amulets and Rings", "Amulets and Rings is always highlighted.", 7, 0)]
        public ToggleNode Ignore1 { get; set; } = new ToggleNode(false);

        [Menu("Separate stash tabs for each item type",
            "Pick priority will be calculated by free space in stash tab. Free space will be calculated for each item stash tab.")]
        public ToggleNode CalcByFreeSpace { get; set; } = new ToggleNode(false);

        [Menu("Ignore Elder/Shaper items")]
        public ToggleNode IgnoreElderShaper { get; set; } = new ToggleNode(true);
        [Menu("Show Red Rectangle Around Ignored Items")]
        public ToggleNode ShowRedRectangleAroundIgnoredItems { get; set; } = new ToggleNode(true);
        [Menu("Auto sell on keypress at vendor?")]
        public ToggleNode AutoSell { get; set; } = new ToggleNode(true);

        [Menu("Draw labels in inventory while any item is hovered?")]
        public ToggleNode LablesWhileHovered { get; set; } = new ToggleNode(true);
        public ToggleNode Enable { get; set; } = new ToggleNode(false);

        [Menu("Only Allowed Stash Tabs", "Define stash tabs manually to ignore other tabs")]
        public ToggleNode OnlyAllowedStashTabs { get; set; } = new ToggleNode(false);
    }
}
