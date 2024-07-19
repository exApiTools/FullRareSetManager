using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using ExileCore;
using Newtonsoft.Json;

namespace FullRareSetManager
{
    public class StashData
    {
        private const string StashDataFile = "StashData.json";
        public StashTabData PlayerInventory = new StashTabData();

        [JsonIgnore]
        public Dictionary<(int, int?), StashTabData> StashTabs { get; private set; } = new();

        [JsonProperty]
        private IReadOnlyDictionary<string, StashTabData> StoredStashTabs
        {
            get { return StashTabs.ToImmutableDictionary(x => $"{x.Key.Item1}/{x.Key.Item2}", x => x.Value); }
            set
            {
                StashTabs = value.ToDictionary(x => x.Key.Split('/') switch
                    {
                        { Length: 2 } a => (int.Parse(a[0]), string.IsNullOrEmpty(a[1]) ? (int?)null : int.Parse(a[1]))
                    },
                    x => x.Value);
            }
        }

        public static StashData Load(FullRareSetManagerCore plugin)
        {
            try
            {
                var dataFileFullPath = Path.Join(plugin.ConfigDirectory, StashDataFile);

                if (!File.Exists(dataFileFullPath))
                {
                    var result = new StashData();
                    Save(plugin, result);
                    return result;
                }

                var json = File.ReadAllText(dataFileFullPath);
                return JsonConvert.DeserializeObject<StashData>(json);
            }
            catch (Exception ex)
            {
                DebugWindow.LogError(
                    $"RareSetManager: Can't load cached items from file StashData.json. Creating new config. " +
                    $"Open stash tabs for updating info. Tell the developer if this happen often enough. {ex}",
                    10);
                return new StashData();
            }
        }

        public static void Save(FullRareSetManagerCore plugin, StashData data)
        {
            try
            {
                if (data == null) return;
                var dataFileFullPath = Path.Join(plugin.ConfigDirectory, StashDataFile);
                var settingsDirName = Path.GetDirectoryName(dataFileFullPath);
                Directory.CreateDirectory(settingsDirName);

                using var stream = new StreamWriter(File.Create(dataFileFullPath));
                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                stream.Write(json);
            }
            catch (Exception ex)
            {
                DebugWindow.LogError($"Unable to save settings: {ex}");
            }
        }
        public static bool Reset(FullRareSetManagerCore plugin)
        {
            try
            {
                var dataFileFullPath = Path.Join(plugin.ConfigDirectory, StashDataFile);
                if (!File.Exists(dataFileFullPath))
                {
                    return true;
                }
                using var stream = new StreamWriter(File.Create(dataFileFullPath));
                var json = JsonConvert.SerializeObject(new StashData(), Formatting.Indented);
                stream.Write(json);
                return true;
            }
            catch (Exception ex)
            {
                DebugWindow.LogError(
                    $"Cannot reset StashData:\n {ex}",
                    10);
                return false;
            }

        }
    }

    public class StashTabData
    {
        public List<StashItem> StashTabItems = new List<StashItem>();
    }

    public class StashItem
    {
        public int InventPosX;
        public int InventPosY;
        public string ItemClass;
        public string ItemName;
        public StashItemType ItemType;
        public int Width;
        public int Height;
        public bool LowLvl;
        public string StashName;
        public int StashIndex;
        public int? NestedStashIndex;
        public bool BInPlayerInventory { get; set; }
    }

    public enum StashItemType
    {
        Undefined = -1,
        Weapon = 0,
        Helmet = 1,
        Body = 2,
        Gloves = 3,
        Boots = 4,
        Belt = 5,
        Amulet = 6,
        Ring = 7,
        TwoHanded = 8,
        OneHanded = 9,
    }
}