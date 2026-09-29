using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using NOModFramework;
using Rewired;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TargetFilterPresets
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("com.JUSTJ7780.nomodframework", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class TargetFilterPresetsPlugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "com.JUSTJ7780.targetfilterpresets";
        internal const string PluginName = "Target Filter Presets";
        internal const string PluginVersion = "1.5.0";
        internal const int DefaultPresetCount = 1;
        internal const int MaxPresetCount = 20;

        private static TargetFilterPresetsPlugin _instance;
        private static ConfigEntry<int> _selectedPreset;
        private static ConfigEntry<int> _presetSlots;
        private static readonly ConfigEntry<string>[] _presetNames = new ConfigEntry<string>[MaxPresetCount];
        private static readonly ConfigEntry<string>[] _presetEntries = new ConfigEntry<string>[MaxPresetCount];
        private readonly ConfigEntry<KeyboardShortcut>[] _applyKeys = new ConfigEntry<KeyboardShortcut>[MaxPresetCount];
        private readonly ConfigEntry<string>[] _applyHotasKeys = new ConfigEntry<string>[MaxPresetCount];
        private ConfigEntry<KeyboardShortcut> _nextPresetKey;
        private ConfigEntry<KeyboardShortcut> _previousPresetKey;
        private ConfigEntry<string> _nextPresetHotasKey;
        private ConfigEntry<string> _previousPresetHotasKey;
        private TargetListSelector _cachedSelector;
        private TargetFilterPresetsMenu _menu;
        private TargetPresetHudMessage _hudMessage;

        internal bool SuppressShortcuts { get; set; }

        internal static TargetFilterPresetsPlugin Instance
        {
            get { return _instance; }
        }

        internal static int SelectedPresetIndex
        {
            get
            {
                if (_selectedPreset == null)
                    return 0;

                return Mathf.Clamp(_selectedPreset.Value - 1, 0, ActivePresetCount - 1);
            }
            set
            {
                if (_selectedPreset != null)
                    _selectedPreset.Value = Mathf.Clamp(value, 0, ActivePresetCount - 1) + 1;
            }
        }

        internal static int ActivePresetCount
        {
            get
            {
                if (_presetSlots == null)
                    return DefaultPresetCount;

                return Mathf.Clamp(_presetSlots.Value, 1, MaxPresetCount);
            }
        }

        //ugg add some logging here

        private void Awake()
        {
            _instance = this;
            BindConfig();
            DontDestroyOnLoad(gameObject);
            _hudMessage = gameObject.AddComponent<TargetPresetHudMessage>();
            _menu = new TargetFilterPresetsMenu();
            ModMenuAPI.Register(_menu);
            Logger.LogInfo("Target Filter Presets loaded");
        }

        private void OnDestroy()
        {
            ModMenuAPI.Unregister(PluginGuid);
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (SuppressShortcuts)
                return;

            int activeCount = ActivePresetCount;
            for (int i = 0; i < activeCount; i++)
            {
                if (_applyKeys[i].Value.IsDown() || HotasShortcut.Deserialize(_applyHotasKeys[i].Value).IsDown())
                {
                    SelectedPresetIndex = i;
                    ApplyPreset(i);
                }
            }

            if (_nextPresetKey.Value.IsDown() || HotasShortcut.Deserialize(_nextPresetHotasKey.Value).IsDown())
            {
                int next = (SelectedPresetIndex + 1) % activeCount;
                SelectedPresetIndex = next;
                ApplyPreset(next);
            }

            if (_previousPresetKey.Value.IsDown() || HotasShortcut.Deserialize(_previousPresetHotasKey.Value).IsDown())
            {
                int previous = SelectedPresetIndex - 1;
                if (previous < 0)
                    previous = activeCount - 1;

                SelectedPresetIndex = previous;
                ApplyPreset(previous);
            }
        }

        private void BindConfig()
        {
            _selectedPreset = Config.Bind("General", "Selected Preset", 1, new ConfigDescription("Preset selected in the framework menu.", new AcceptableValueRange<int>(1, MaxPresetCount)));
            _presetSlots = Config.Bind("General", "Preset Slots", DefaultPresetCount, new ConfigDescription("Number of preset slots shown in the menu.", new AcceptableValueRange<int>(1, MaxPresetCount)));

            for (int i = 0; i < MaxPresetCount; i++)
            {
                int presetNumber = i + 1;
                _presetNames[i] = Config.Bind(
                    "Preset Names",
                    "Preset " + presetNumber + " Name",
                    "Preset " + presetNumber,
                    "Display name for preset " + presetNumber + ".");

                _presetEntries[i] = Config.Bind(
                    "Presets",
                    "Preset " + presetNumber,
                    FilterPreset.Default.Serialize(),
                    "Saved target filter state for preset " + presetNumber + ".");

                _applyKeys[i] = Config.Bind(
                    "Keybinds",
                    "Apply Preset " + presetNumber,
                    GetDefaultApplyShortcut(i),
                    "Applies target filter preset " + presetNumber + ".");

                _applyHotasKeys[i] = Config.Bind(
                    "HOTAS Keybinds",
                    "Apply Preset " + presetNumber,
                    string.Empty,
                    "Optional Rewired joystick/HOTAS button for applying target filter preset " + presetNumber + ".");
            }

            _nextPresetKey = Config.Bind("Keybinds", "Apply Next Preset", new KeyboardShortcut(KeyCode.RightBracket, KeyCode.LeftAlt), "Cycles to the next target filter preset and applies it.");
            _previousPresetKey = Config.Bind("Keybinds", "Apply Previous Preset", new KeyboardShortcut(KeyCode.LeftBracket, KeyCode.LeftAlt), "Cycles to the previous target filter preset and applies it.");
            _nextPresetHotasKey = Config.Bind("HOTAS Keybinds", "Apply Next Preset", string.Empty, "Optional Rewired joystick/HOTAS button for cycling to the next target filter preset.");
            _previousPresetHotasKey = Config.Bind("HOTAS Keybinds", "Apply Previous Preset", string.Empty, "Optional Rewired joystick/HOTAS button for cycling to the previous target filter preset.");
        }

        internal bool AddBlankPreset()
        {
            int count = ActivePresetCount;
            if (count >= MaxPresetCount)
                return false;

            int newIndex = count;
            _presetSlots.Value = count + 1;
            _presetNames[newIndex].Value = "Preset " + (newIndex + 1);
            _presetEntries[newIndex].Value = FilterPreset.Blank.Serialize();
            _applyKeys[newIndex].Value = KeyboardShortcut.Empty;
            _applyHotasKeys[newIndex].Value = string.Empty;
            SelectedPresetIndex = newIndex;
            return true;
        }

        internal string[] GetPresetDisplayNames()
        {
            int count = ActivePresetCount;
            string[] names = new string[count];
            for (int i = 0; i < count; i++)
                names[i] = GetPresetDropdownName(i);

            return names;
        }

        internal string GetPresetName(int presetIndex)
        {
            presetIndex = Mathf.Clamp(presetIndex, 0, MaxPresetCount - 1);
            return _presetNames[presetIndex].Value ?? string.Empty;
        }

        internal string GetPresetDropdownName(int presetIndex)
        {
            string name = GetPresetName(presetIndex);
            if (name.Length == 0)
                return " ";

            return name;
        }

        internal void SetPresetName(int presetIndex, string name)
        {
            presetIndex = Mathf.Clamp(presetIndex, 0, MaxPresetCount - 1);
            if (name == null)
                name = string.Empty;

            _presetNames[presetIndex].Value = name;
        }

        internal string GetApplyShortcutText(int presetIndex)
        {
            presetIndex = Mathf.Clamp(presetIndex, 0, MaxPresetCount - 1);
            return BuildBindingDisplay(_applyKeys[presetIndex].Value, _applyHotasKeys[presetIndex].Value);
        }

        internal void SetApplyShortcut(int presetIndex, KeyboardShortcut shortcut)
        {
            presetIndex = Mathf.Clamp(presetIndex, 0, MaxPresetCount - 1);
            _applyKeys[presetIndex].Value = shortcut;
            _applyHotasKeys[presetIndex].Value = string.Empty;
        }

        internal void SetApplyShortcut(int presetIndex, HotasShortcut shortcut)
        {
            presetIndex = Mathf.Clamp(presetIndex, 0, MaxPresetCount - 1);
            _applyKeys[presetIndex].Value = KeyboardShortcut.Empty;
            _applyHotasKeys[presetIndex].Value = shortcut.Serialize();
        }

        internal string GetNextPresetShortcutText()
        {
            return BuildBindingDisplay(_nextPresetKey.Value, _nextPresetHotasKey.Value);
        }

        internal string GetPreviousPresetShortcutText()
        {
            return BuildBindingDisplay(_previousPresetKey.Value, _previousPresetHotasKey.Value);
        }

        internal void SetNextPresetShortcut(KeyboardShortcut shortcut)
        {
            _nextPresetKey.Value = shortcut;
            _nextPresetHotasKey.Value = string.Empty;
        }

        internal void SetPreviousPresetShortcut(KeyboardShortcut shortcut)
        {
            _previousPresetKey.Value = shortcut;
            _previousPresetHotasKey.Value = string.Empty;
        }

        internal void SetNextPresetShortcut(HotasShortcut shortcut)
        {
            _nextPresetKey.Value = KeyboardShortcut.Empty;
            _nextPresetHotasKey.Value = shortcut.Serialize();
        }

        internal void SetPreviousPresetShortcut(HotasShortcut shortcut)
        {
            _previousPresetKey.Value = KeyboardShortcut.Empty;
            _previousPresetHotasKey.Value = shortcut.Serialize();
        }

        internal bool ApplyPreset(int presetIndex)
        {
            TargetListSelector selector = FindSelector();
            if (selector == null)
            {
                Logger.LogWarning("No TargetListSelector is active; open the target selection page first.");
                return false;
            }

            FilterPreset preset = GetPreset(presetIndex);
            ApplyPresetToSelector(selector, preset);
            ShowPresetMessage(presetIndex);
            Logger.LogInfo("Applied target filter preset " + (presetIndex + 1));
            return true;
        }

        internal bool CapturePreset(int presetIndex)
        {
            TargetListSelector selector = FindSelector();
            if (selector == null)
            {
                Logger.LogWarning("No TargetListSelector is active; open the target selection page first.");
                return false;
            }

            FilterPreset preset = FilterPreset.FromSelector(selector);
            SavePreset(presetIndex, preset);
            Logger.LogInfo("Saved target filter preset " + (presetIndex + 1));
            return true;
        }

        internal FilterPreset GetPreset(int presetIndex)
        {
            presetIndex = Mathf.Clamp(presetIndex, 0, MaxPresetCount - 1);
            return FilterPreset.Deserialize(_presetEntries[presetIndex].Value);
        }

        internal void SavePreset(int presetIndex, FilterPreset preset)
        {
            presetIndex = Mathf.Clamp(presetIndex, 0, MaxPresetCount - 1);
            _presetEntries[presetIndex].Value = preset.Serialize();
        }

        internal bool HasActiveSelector()
        {
            return FindSelector() != null;
        }

        internal string GetPresetSummary(int presetIndex)
        {
            return GetPreset(presetIndex).GetSummary();
        }

        internal void SetPresetFlag(int presetIndex, PresetGroup group, int flagIndex, bool value)
        {
            FilterPreset preset = GetPreset(presetIndex);
            preset.SetFlag(group, flagIndex, value);
            SavePreset(presetIndex, preset);
        }

        internal bool GetPresetFlag(int presetIndex, PresetGroup group, int flagIndex)
        {
            return GetPreset(presetIndex).GetFlag(group, flagIndex);
        }

        private TargetListSelector FindSelector()
        {
            if (_cachedSelector != null && _cachedSelector.isActiveAndEnabled)
                return _cachedSelector;

            _cachedSelector = Object.FindObjectOfType<TargetListSelector>();
            return _cachedSelector;
        }

        private void ShowPresetMessage(int presetIndex)
        {
            if (_hudMessage == null)
                return;

            string name = GetPresetName(presetIndex);
            if (string.IsNullOrWhiteSpace(name))
                name = "Preset " + (presetIndex + 1);

            _hudMessage.Show("TARGET FILTER: " + name, 1.5f);
        }

        private static KeyboardShortcut GetDefaultApplyShortcut(int index)
        {
            if (index >= 0 && index < 8)
                return new KeyboardShortcut((KeyCode)((int)KeyCode.F5 + index));

            return KeyboardShortcut.Empty;
        }

        private static string BuildBindingDisplay(KeyboardShortcut keyboard, string hotasRaw)
        {
            HotasShortcut hotas = HotasShortcut.Deserialize(hotasRaw);
            if (hotas.IsBound)
                return hotas.DisplayName;

            string text = keyboard.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return text;
        }

        private static void ApplyPresetToSelector(TargetListSelector selector, FilterPreset preset)
        {
            if (selector.toggleFollowHUD != null)
                selector.toggleFollowHUD.Set(preset.FollowHud);

            if (selector.toggleLaser != null)
                selector.toggleLaser.Set(preset.Laser);

            ApplyBits(selector.toggleFactionItems, preset.FactionBits);
            ApplyBits(selector.toggleUnitTypesItems, preset.UnitBits);
            ApplyBits(selector.toggleVehicleTypesItems, preset.VehicleBits);

            selector.CheckAllExclusions();
            selector.NeedUpdateIcons();
        }

        private static void ApplyBits(List<TargetListSelector_ToggleButton> buttons, string bits)
        {
            if (buttons == null)
                return;

            for (int i = 0; i < buttons.Count; i++)
            {
                bool value = i < bits.Length && bits[i] == '1';
                if (buttons[i] != null)
                    buttons[i].Set(value);
            }
        }
    }

    internal struct HotasShortcut
    {
        public int ControllerId;
        public int ElementId;
        public string ControllerName;
        public string ElementName;

        public bool IsBound
        {
            get { return ElementId >= 0; }
        }

        public string DisplayName
        {
            get
            {
                if (!IsBound)
                    return string.Empty;

                string controller = string.IsNullOrEmpty(ControllerName) ? "HOTAS" : ControllerName;
                string element = string.IsNullOrEmpty(ElementName) ? "Button " + ElementId : ElementName;
                return controller + " / " + element;
            }
        }

        public bool IsDown()
        {
            if (!IsBound || !ReInput.isReady)
                return false;

            Joystick joystick = FindJoystick();
            return joystick != null && joystick.GetButtonDownById(ElementId);
        }

        public string Serialize()
        {
            if (!IsBound)
                return string.Empty;

            return "hotas|" + ControllerId + "|" + ElementId + "|" + Escape(ControllerName) + "|" + Escape(ElementName);
        }

        public static HotasShortcut Empty
        {
            get
            {
                return new HotasShortcut
                {
                    ControllerId = -1,
                    ElementId = -1,
                    ControllerName = string.Empty,
                    ElementName = string.Empty
                };
            }
        }

        public static HotasShortcut Deserialize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return Empty;

            string[] parts = raw.Split('|');
            if (parts.Length < 3 || !parts[0].Equals("hotas", StringComparison.OrdinalIgnoreCase))
                return Empty;

            int controllerId;
            int elementId;
            if (!int.TryParse(parts[1], out controllerId) || !int.TryParse(parts[2], out elementId))
                return Empty;

            return new HotasShortcut
            {
                ControllerId = controllerId,
                ElementId = elementId,
                ControllerName = parts.Length > 3 ? Unescape(parts[3]) : string.Empty,
                ElementName = parts.Length > 4 ? Unescape(parts[4]) : string.Empty
            };
        }

        public static bool TryCapture(out HotasShortcut shortcut)
        {
            shortcut = Empty;

            if (!ReInput.isReady)
                return false;

            Player player = ReInput.players.GetPlayer(0);
            if (player == null || player.controllers == null || player.controllers.Joysticks == null)
                return false;

            IList<Joystick> joysticks = player.controllers.Joysticks;
            for (int i = 0; i < joysticks.Count; i++)
            {
                Joystick joystick = joysticks[i];
                if (joystick == null)
                    continue;

                ControllerPollingInfo info = joystick.PollForFirstButtonDown();
                if (!info.success)
                    continue;

                shortcut = new HotasShortcut
                {
                    ControllerId = joystick.id,
                    ElementId = info.elementIdentifierId,
                    ControllerName = joystick.name,
                    ElementName = info.elementIdentifierName
                };
                return true;
            }

            return false;
        }

        private Joystick FindJoystick()
        {
            Player player = ReInput.players.GetPlayer(0);
            if (player == null || player.controllers == null || player.controllers.Joysticks == null)
                return null;

            IList<Joystick> joysticks = player.controllers.Joysticks;
            for (int i = 0; i < joysticks.Count; i++)
            {
                Joystick joystick = joysticks[i];
                if (joystick == null)
                    continue;

                if (joystick.id == ControllerId)
                    return joystick;
            }

            for (int i = 0; i < joysticks.Count; i++)
            {
                Joystick joystick = joysticks[i];
                if (joystick != null && string.Equals(joystick.name, ControllerName, StringComparison.OrdinalIgnoreCase))
                    return joystick;
            }

            return null;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("%", "%25").Replace("|", "%7C");
        }

        private static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("%7C", "|").Replace("%25", "%");
        }
    }

    internal enum PresetGroup
    {
        Faction,
        Unit,
        Vehicle
    }

    internal sealed class FilterPreset
    {
        public bool FollowHud;
        public bool Laser;
        public string FactionBits;
        public string UnitBits;
        public string VehicleBits;

        public static FilterPreset Default
        {
            get
            {
                return new FilterPreset
                {
                    FollowHud = false,
                    Laser = false,
                    FactionBits = "01",
                    UnitBits = "11111",
                    VehicleBits = "111111111111"
                };
            }
        }

        public static FilterPreset Blank
        {
            get
            {
                return new FilterPreset
                {
                    FollowHud = false,
                    Laser = false,
                    FactionBits = "00",
                    UnitBits = "00000",
                    VehicleBits = "000000000000"
                };
            }
        }

        public static FilterPreset FromSelector(TargetListSelector selector)
        {
            FilterPreset preset = new FilterPreset();
            preset.FollowHud = selector.toggleFollowHUD != null && selector.toggleFollowHUD.status;
            preset.Laser = selector.toggleLaser != null && selector.toggleLaser.status;
            preset.FactionBits = CaptureBits(selector.toggleFactionItems);
            preset.UnitBits = CaptureBits(selector.toggleUnitTypesItems);
            preset.VehicleBits = CaptureBits(selector.toggleVehicleTypesItems);
            return preset;
        }

        public static FilterPreset Deserialize(string raw)
        {
            FilterPreset preset = Default;

            if (string.IsNullOrEmpty(raw))
                return preset;

            string[] parts = raw.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                string[] pair = parts[i].Split(new[] { '=' }, 2);
                if (pair.Length != 2)
                    continue;

                string key = pair[0].Trim().ToLowerInvariant();
                string value = pair[1].Trim();

                if (key == "follow")
                    preset.FollowHud = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                else if (key == "laser")
                    preset.Laser = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                else if (key == "faction")
                    preset.FactionBits = CleanBits(value);
                else if (key == "unit")
                    preset.UnitBits = CleanBits(value);
                else if (key == "vehicle")
                    preset.VehicleBits = CleanBits(value);
            }

            return preset;
        }

        public string Serialize()
        {
            return "follow=" + BoolBit(FollowHud) +
                   "|laser=" + BoolBit(Laser) +
                   "|faction=" + CleanBits(FactionBits) +
                   "|unit=" + CleanBits(UnitBits) +
                   "|vehicle=" + CleanBits(VehicleBits);
        }

        public string GetSummary()
        {
            return "Faction " + CountEnabled(FactionBits) +
                   ", Type " + CountEnabled(UnitBits) +
                   ", Vehicle " + CountEnabled(VehicleBits) +
                   ", Laser " + (Laser ? "on" : "off");
        }

        public bool GetFlag(PresetGroup group, int flagIndex)
        {
            string bits = GetBits(group);
            return flagIndex >= 0 && flagIndex < bits.Length && bits[flagIndex] == '1';
        }

        public void SetFlag(PresetGroup group, int flagIndex, bool value)
        {
            if (flagIndex < 0)
                return;

            string bits = GetBits(group);
            while (bits.Length <= flagIndex)
                bits += "0";

            char[] chars = bits.ToCharArray();
            chars[flagIndex] = value ? '1' : '0';
            SetBits(group, new string(chars));
        }

        private string GetBits(PresetGroup group)
        {
            if (group == PresetGroup.Faction)
                return FactionBits ?? string.Empty;
            if (group == PresetGroup.Unit)
                return UnitBits ?? string.Empty;

            return VehicleBits ?? string.Empty;
        }

        private void SetBits(PresetGroup group, string bits)
        {
            if (group == PresetGroup.Faction)
                FactionBits = bits;
            else if (group == PresetGroup.Unit)
                UnitBits = bits;
            else
                VehicleBits = bits;
        }

        private static string CaptureBits(List<TargetListSelector_ToggleButton> buttons)
        {
            if (buttons == null)
                return string.Empty;

            char[] bits = new char[buttons.Count];
            for (int i = 0; i < buttons.Count; i++)
                bits[i] = buttons[i] != null && buttons[i].status ? '1' : '0';

            return new string(bits);
        }

        private static string CleanBits(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return string.Empty;

            char[] buffer = new char[raw.Length];
            int count = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == '0' || raw[i] == '1')
                    buffer[count++] = raw[i];
            }

            return new string(buffer, 0, count);
        }

        private static string BoolBit(bool value)
        {
            return value ? "1" : "0";
        }

        private static int CountEnabled(string bits)
        {
            int count = 0;
            if (bits == null)
                return count;

            for (int i = 0; i < bits.Length; i++)
            {
                if (bits[i] == '1')
                    count++;
            }

            return count;
        }
    }
}
