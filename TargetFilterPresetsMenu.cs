using BepInEx.Configuration;
using NOModFramework;
using UnityEngine;

namespace TargetFilterPresets
{
    internal sealed class TargetFilterPresetsMenu : IModMenu
    {
        private const string MenuId = TargetFilterPresetsPlugin.PluginGuid;
        private bool _showActions = true;
        private bool _showNaming;
        private bool _showKeybinds;
        private bool _showFilters;
        private int _draftPresetIndex = -1;
        private string _shortcutStatus = string.Empty;
        private CaptureTarget _captureTarget = CaptureTarget.None;

        public string Id
        {
            get { return MenuId; }
        }

        public string DisplayName
        {
            get { return "Target Filter Presets"; }
        }

        public Texture2D Icon
        {
            get { return null; }
        }

        public int SortOrder
        {
            get { return 120; }
        }

        public void OnMenuOpen()
        {
        }

        public void OnMenuClose()
        {
            TargetFilterPresetsPlugin plugin = TargetFilterPresetsPlugin.Instance;
            if (plugin != null)
                plugin.SuppressShortcuts = false;

            _captureTarget = CaptureTarget.None;
        }

        public void Draw(ModUiContext ui)
        {
            TargetFilterPresetsPlugin plugin = TargetFilterPresetsPlugin.Instance;
            if (plugin == null)
            {
                ui.Widgets.Label("Target Filter Presets is not ready yet.");
                return;
            }

            plugin.SuppressShortcuts = _captureTarget != CaptureTarget.None;
            HandleShortcutCapture(plugin, TargetFilterPresetsPlugin.SelectedPresetIndex);

            ui.Widgets.Section("Target Filters");

            string[] presets = plugin.GetPresetDisplayNames();
            int selected = TargetFilterPresetsPlugin.SelectedPresetIndex;
            int newSelected = ui.Widgets.Dropdown("Preset", selected, presets);
            if (newSelected >= 0 && newSelected < TargetFilterPresetsPlugin.ActivePresetCount && newSelected != selected)
            {
                TargetFilterPresetsPlugin.SelectedPresetIndex = newSelected;
                SyncShortcutDrafts(plugin, newSelected);
            }

            int presetIndex = TargetFilterPresetsPlugin.SelectedPresetIndex;

            DrawFoldout(ui, "Actions", ref _showActions);
            if (_showActions)
                DrawActions(ui, plugin, presetIndex);

            DrawFoldout(ui, "Rename Preset", ref _showNaming);
            if (_showNaming)
                DrawRename(ui, plugin, presetIndex);

            DrawFoldout(ui, "Keybinds", ref _showKeybinds);
            if (_showKeybinds)
                DrawKeybinds(ui, plugin, presetIndex);

            DrawFoldout(ui, "Filter Editor", ref _showFilters);
            if (_showFilters)
            {
                DrawFlagGroup(ui, plugin, presetIndex, PresetGroup.Faction, "Faction", new[] { "Friendly", "Enemy" });
                DrawFlagGroup(ui, plugin, presetIndex, PresetGroup.Unit, "Category", new[] { "Air", "MSL", "GND", "BLD", "SHP" });
                DrawFlagGroup(ui, plugin, presetIndex, PresetGroup.Vehicle, "Vehicle", new[] { "Truck", "UGV", "LCV", "AFV", "MBT", "ART", "AA", "IR SAM", "R SAM", "RDR" });
            }
        }

        private void DrawActions(ModUiContext ui, TargetFilterPresetsPlugin plugin, int presetIndex)
        {
            BeginPanel(ui);

            if (ui.Widgets.Button("Apply Selected Preset", GUILayout.Height(30f)))
                plugin.ApplyPreset(presetIndex);

            if (ui.Widgets.Button("Save Current Filters", GUILayout.Height(30f)))
                plugin.CapturePreset(presetIndex);

            if (ui.Widgets.Button("Add Blank Preset", GUILayout.Height(30f)))
                plugin.AddBlankPreset();

            EndPanel();
        }

        private void DrawRename(ModUiContext ui, TargetFilterPresetsPlugin plugin, int presetIndex)
        {
            BeginPanel(ui);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", ui.Widgets.LabelStyle, GUILayout.Width(150f), GUILayout.Height(28f));
            string currentName = plugin.GetPresetName(presetIndex);
            string newName = GUILayout.TextField(currentName, 32, GUILayout.ExpandWidth(true), GUILayout.Height(28f));
            GUILayout.EndHorizontal();

            if (newName != currentName)
                plugin.SetPresetName(presetIndex, newName);

            EndPanel();
        }

        private void DrawKeybinds(ModUiContext ui, TargetFilterPresetsPlugin plugin, int presetIndex)
        {
            SyncShortcutDrafts(plugin, presetIndex);
            BeginPanel(ui);

            DrawCaptureRow(ui, "Apply", CaptureTarget.ApplyPreset, plugin.GetApplyShortcutText(presetIndex));
            DrawCaptureRow(ui, "Cycle Next", CaptureTarget.NextPreset, plugin.GetNextPresetShortcutText());
            DrawCaptureRow(ui, "Cycle Previous", CaptureTarget.PreviousPreset, plugin.GetPreviousPresetShortcutText());

            if (!string.IsNullOrEmpty(_shortcutStatus))
                ui.Widgets.Label(_shortcutStatus);

            EndPanel();
        }

        // thanks ugg

        private void DrawCaptureRow(ModUiContext ui, string label, CaptureTarget target, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, ui.Widgets.LabelStyle, GUILayout.Width(150f), GUILayout.Height(28f));
            string buttonText = _captureTarget == target ? "Press key combo..." : DisplayShortcut(value);
            if (ui.Widgets.Button(buttonText, GUILayout.ExpandWidth(true), GUILayout.Height(28f)))
            {
                _captureTarget = target;
            _shortcutStatus = "Press a keyboard combo or HOTAS button. Escape clears.";
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawFlagGroup(ModUiContext ui, TargetFilterPresetsPlugin plugin, int presetIndex, PresetGroup group, string title, string[] labels)
        {
            ui.Widgets.Section(title);
            BeginPanel(ui);
            for (int i = 0; i < labels.Length; i++)
            {
                bool current = plugin.GetPresetFlag(presetIndex, group, i);
                bool changed = ui.Widgets.Toggle(labels[i], current);
                if (changed != current)
                    plugin.SetPresetFlag(presetIndex, group, i, changed);
            }
            EndPanel();
        }

        private void DrawFoldout(ModUiContext ui, string title, ref bool expanded)
        {
            ui.Widgets.Spacer(6f);
            string marker = expanded ? "v " : "> ";
            if (ui.Widgets.Button(marker + title, GUILayout.Height(30f)))
                expanded = !expanded;
        }

        private void SyncShortcutDrafts(TargetFilterPresetsPlugin plugin, int presetIndex)
        {
            if (_draftPresetIndex == presetIndex)
                return;

            _draftPresetIndex = presetIndex;
            _shortcutStatus = string.Empty;
        }

        private void HandleShortcutCapture(TargetFilterPresetsPlugin plugin, int presetIndex)
        {
            if (_captureTarget == CaptureTarget.None)
                return;

            Event current = Event.current;
            HotasShortcut hotasShortcut;
            if (HotasShortcut.TryCapture(out hotasShortcut))
            {
                ApplyCapturedShortcut(plugin, presetIndex, hotasShortcut);
                _shortcutStatus = "Keybind saved: " + hotasShortcut.DisplayName;
                _captureTarget = CaptureTarget.None;
                plugin.SuppressShortcuts = false;
                return;
            }

            if (current == null || current.type != EventType.KeyDown)
                return;

            if (current.keyCode == KeyCode.None || IsModifier(current.keyCode))
                return;

            KeyboardShortcut shortcut;
            if (current.keyCode == KeyCode.Escape || current.keyCode == KeyCode.Backspace || current.keyCode == KeyCode.Delete)
                shortcut = KeyboardShortcut.Empty;
            else
                shortcut = BuildShortcut(current);

            ApplyCapturedShortcut(plugin, presetIndex, shortcut);

            _shortcutStatus = shortcut.Equals(KeyboardShortcut.Empty) ? "Keybind cleared." : "Keybind saved: " + shortcut;
            _captureTarget = CaptureTarget.None;
            plugin.SuppressShortcuts = false;
            current.Use();
        }

        private void ApplyCapturedShortcut(TargetFilterPresetsPlugin plugin, int presetIndex, KeyboardShortcut shortcut)
        {
            if (_captureTarget == CaptureTarget.ApplyPreset)
                plugin.SetApplyShortcut(presetIndex, shortcut);
            else if (_captureTarget == CaptureTarget.NextPreset)
                plugin.SetNextPresetShortcut(shortcut);
            else if (_captureTarget == CaptureTarget.PreviousPreset)
                plugin.SetPreviousPresetShortcut(shortcut);
        }

        private void ApplyCapturedShortcut(TargetFilterPresetsPlugin plugin, int presetIndex, HotasShortcut shortcut)
        {
            if (_captureTarget == CaptureTarget.ApplyPreset)
                plugin.SetApplyShortcut(presetIndex, shortcut);
            else if (_captureTarget == CaptureTarget.NextPreset)
                plugin.SetNextPresetShortcut(shortcut);
            else if (_captureTarget == CaptureTarget.PreviousPreset)
                plugin.SetPreviousPresetShortcut(shortcut);
        }

        private static KeyboardShortcut BuildShortcut(Event current)
        {
            if (current.control && current.alt && current.shift)
                return new KeyboardShortcut(current.keyCode, KeyCode.LeftControl, KeyCode.LeftAlt, KeyCode.LeftShift);
            if (current.control && current.alt)
                return new KeyboardShortcut(current.keyCode, KeyCode.LeftControl, KeyCode.LeftAlt);
            if (current.control && current.shift)
                return new KeyboardShortcut(current.keyCode, KeyCode.LeftControl, KeyCode.LeftShift);
            if (current.alt && current.shift)
                return new KeyboardShortcut(current.keyCode, KeyCode.LeftAlt, KeyCode.LeftShift);
            if (current.control)
                return new KeyboardShortcut(current.keyCode, KeyCode.LeftControl);
            if (current.alt)
                return new KeyboardShortcut(current.keyCode, KeyCode.LeftAlt);
            if (current.shift)
                return new KeyboardShortcut(current.keyCode, KeyCode.LeftShift);

            return new KeyboardShortcut(current.keyCode);
        }

        private static bool IsModifier(KeyCode key)
        {
            return key == KeyCode.LeftControl ||
                   key == KeyCode.RightControl ||
                   key == KeyCode.LeftAlt ||
                   key == KeyCode.RightAlt ||
                   key == KeyCode.LeftShift ||
                   key == KeyCode.RightShift ||
                   key == KeyCode.LeftCommand ||
                   key == KeyCode.RightCommand;
        }

        private static string DisplayShortcut(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unbound";

            return value;
        }

        private static void BeginPanel(ModUiContext ui)
        {
            GUILayout.BeginVertical(ui.Widgets.PanelAltStyle, GUILayout.ExpandWidth(true));
        }

        private static void EndPanel()
        {
            GUILayout.EndVertical();
        }

        private enum CaptureTarget
        {
            None,
            ApplyPreset,
            NextPreset,
            PreviousPreset
        }
    }
}
