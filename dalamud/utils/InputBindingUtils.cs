using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using static FFXIVClientStructs.FFXIV.Client.UI.Misc.ConfigModule;

namespace MgAl2O4.Utils
{
    public struct InputBindingKey
    {
        public GamepadButtons gamepadButton;
        public VirtualKey virtualKey;

        public FontAwesomeIcon icon;
        public string text;
        public float scaleOverride;

        public bool IsValid()
        {
            return (gamepadButton != GamepadButtons.None) || (virtualKey != VirtualKey.NO_KEY);
        }
    }

    public struct InputBindingChord
    {
        public readonly InputBindingKey[] keys;
        public readonly GamepadButtons mainButton;
        public readonly string simpleText;
        public readonly string separator;

        public InputBindingChord(InputBindingKey[] keys, string separator = " + ")
        {
            this.keys = keys;
            this.separator = separator;
            simpleText = InputBindingUtils.TryCreatingSimpleChordDescription(keys, separator, ignoreScaleOverrides: false);
            mainButton = InputBindingUtils.FindMainGamepadButton(keys);
        }
    }

    public class InputBindingUtils
    {
        public static Dictionary<VirtualKey, string> mapVKAliases = new();

        private static Dictionary<VirtualKey, InputBindingKey> mapVirtualKeys = new();
        private static Dictionary<GamepadButtons, InputBindingKey> mapGamepadButtons = new();

        private static GamepadButtons supportedButtonMask = 0;
        private static readonly GamepadButtons mainButtonMask =
            GamepadButtons.DpadUp | GamepadButtons.DpadDown | GamepadButtons.DpadLeft | GamepadButtons.DpadRight |
            GamepadButtons.North | GamepadButtons.South | GamepadButtons.West | GamepadButtons.East;

        private static bool isUsingXboxGamepadStyle = true;
        private static bool isGamepadStyleInitialized = false;

        public static bool IsUsingXboxGamepadStyle
        {
            get
            {
                if (!isGamepadStyleInitialized)
                {
                    isGamepadStyleInitialized = true;
                    isUsingXboxGamepadStyle = GetGamepadStyleSettings() == 0;
                }

                return isUsingXboxGamepadStyle;
            }
        }

        public static InputBindingKey GetVirtualKeyData(VirtualKey key)
        {
            if (mapVKAliases.TryGetValue(key, out var aliasText))
            {
                return new InputBindingKey() { virtualKey = key, text = aliasText };
            }

            if (mapVirtualKeys.TryGetValue(key, out var keyData))
            {
                return keyData;
            }

            var resultDesc = new StringBuilder();
            uint scanCode = MapVirtualKey((uint)key, 0);
            int lParam = (int)(scanCode << 16);

            GetKeyNameText(lParam, resultDesc, 260);

            var newKeyData = new InputBindingKey() { virtualKey = key, text = resultDesc.ToString() };
            mapVirtualKeys.Add(key, newKeyData);
            return newKeyData;
        }

        public static InputBindingKey GetGamepadButtonData(GamepadButtons button)
        {
            if (mapGamepadButtons.Count == 0)
            {
                InitializeGamepadMap();
            }

            // limit set of supported buttons to what i need right now.
            if ((supportedButtonMask & button) == 0)
            {
                button = GamepadButtons.None;
            }

            if (mapGamepadButtons.TryGetValue(button, out var buttonData))
            {
                return buttonData;
            }

            var newButtonData = new InputBindingKey() { gamepadButton = button, text = "?" };
            mapGamepadButtons.Add(button, newButtonData);
            return newButtonData;
        }

        public static string TryCreatingSimpleChordDescription(InputBindingKey[] keys, string separator, bool ignoreScaleOverrides = true)
        {
            if (keys == null || keys.Length == 0)
            {
                return null;
            }

            string desc = "";
            int numIcons = 0;
            int numCustomScales = 0;

            for (int idx = 0; idx < keys.Length; idx++)
            {
                if (keys[idx].icon != FontAwesomeIcon.None)
                {
                    numIcons = 1;
                    break;
                }

                numCustomScales += (keys[idx].scaleOverride != 1.0f && keys[idx].scaleOverride != 0.0f) ? 1 : 0;

                if (desc.Length > 0) { desc += separator; }
                desc += keys[idx].text;
            }

            bool canUseSimpleDesc = (numIcons == 0) && (ignoreScaleOverrides || numCustomScales == 0);
            return canUseSimpleDesc ? desc : null;
        }

        public static GamepadButtons FindMainGamepadButton(InputBindingKey[] keys)
        {
            if (keys != null)
            {
                foreach (var key in keys)
                {
                    if ((key.gamepadButton & mainButtonMask) != 0)
                    {
                        return key.gamepadButton;
                    }
                }
            }

            return GamepadButtons.None;
        }

        public static void AddToDrawList(ImDrawListPtr drawList, Vector2 pos, uint color, InputBindingChord inputChord)
        {
            if (!string.IsNullOrEmpty(inputChord.simpleText))
            {
                drawList.AddText(pos, color, inputChord.simpleText);
            }
            else if (inputChord.keys != null)
            {
                string sepStr = null;
                foreach (var inputKey in inputChord.keys)
                {
                    if (sepStr != null)
                    {
                        var partSize = ImGui.CalcTextSize(sepStr);
                        drawList.AddText(pos, color, sepStr);
                        pos.X += partSize.X;
                    }
                    else
                    {
                        sepStr = inputChord.separator;
                    }

                    var (textSize, textScale) = CalcInputKeySizeAndScale(inputKey);
                    if (!string.IsNullOrEmpty(inputKey.text))
                    {
                        var offsetY = (textScale > 1.0f) ? textSize.Y * (-textScale + 1.0f) : 0.0f;
                        drawList.AddText(UiBuilder.DefaultFont, ImGui.GetFontSize() * textScale, (offsetY != 0) ? new Vector2(pos.X, pos.Y + offsetY) : pos, color, inputKey.text);
                    }
                    else if (inputKey.icon != FontAwesomeIcon.None)
                    {
                        drawList.AddText(UiBuilder.IconFont, ImGui.GetFontSize() * textScale, pos, color, inputKey.icon.ToIconString());
                    }

                    pos.X += textSize.X * textScale;
                }
            }
        }

        public static (Vector2, float) CalcInputKeySizeAndScale(InputBindingKey inputKey)
        {
            var keyScale = (inputKey.scaleOverride > 0.0f) ? inputKey.scaleOverride : 1.0f;
            if (!string.IsNullOrEmpty(inputKey.text))
            {
                var textSize = ImGui.CalcTextSize(inputKey.text);
                return (textSize, keyScale);
            }
            else if (inputKey.icon != FontAwesomeIcon.None)
            {
                var iconText = inputKey.icon.ToIconString();
                ImGui.PushFont(UiBuilder.IconFont);
                var textSize = ImGui.CalcTextSize(iconText);
                ImGui.PopFont();

                return (textSize, keyScale);
            }

            return (Vector2.Zero, keyScale);
        }

        public static Vector2 CalcInputChordSize(InputBindingChord inputChord)
        {
            Vector2 size = Vector2.Zero;

            if (!string.IsNullOrEmpty(inputChord.simpleText))
            {
                size = ImGui.CalcTextSize(inputChord.simpleText);
            }
            else if (inputChord.keys != null)
            {
                string sepStr = null;
                foreach (var inputKey in inputChord.keys)
                {
                    if (sepStr != null)
                    {
                        var partSize = ImGui.CalcTextSize(sepStr);
                        size.X += partSize.X;
                    }
                    else
                    {
                        sepStr = inputChord.separator;
                    }

                    var (textSize, textScale) = CalcInputKeySizeAndScale(inputKey);
                    size.X += textSize.X * textScale;
                    size.Y = Math.Max(size.Y, textSize.Y * textScale);
                }
            }

            return size;
        }

        private static void InitializeGamepadMap()
        {
            // font is kind of small, but still better than using mountains for triangle :<
            bool useUnicodeActions = true;

            // can't change without client restart
            var styleV = GetGamepadStyleSettings();
            isUsingXboxGamepadStyle = styleV == 0;

            const float scaleOverrideFontAwesome = 0.8f;
            const float scaleOverrideSeIcon = 1.25f;

            mapGamepadButtons.Add(GamepadButtons.DpadUp, new InputBindingKey() { gamepadButton = GamepadButtons.DpadUp, icon = FontAwesomeIcon.ChevronCircleUp, scaleOverride = scaleOverrideFontAwesome });
            mapGamepadButtons.Add(GamepadButtons.DpadDown, new InputBindingKey() { gamepadButton = GamepadButtons.DpadDown, icon = FontAwesomeIcon.ChevronCircleDown, scaleOverride = scaleOverrideFontAwesome });
            mapGamepadButtons.Add(GamepadButtons.DpadRight, new InputBindingKey() { gamepadButton = GamepadButtons.DpadRight, icon = FontAwesomeIcon.ChevronCircleRight, scaleOverride = scaleOverrideFontAwesome });
            mapGamepadButtons.Add(GamepadButtons.DpadLeft, new InputBindingKey() { gamepadButton = GamepadButtons.DpadLeft, icon = FontAwesomeIcon.ChevronCircleLeft, scaleOverride = scaleOverrideFontAwesome });

            if (isUsingXboxGamepadStyle)
            {
                mapGamepadButtons.Add(GamepadButtons.North, new InputBindingKey() { gamepadButton = GamepadButtons.North, text = "Y" });
                mapGamepadButtons.Add(GamepadButtons.South, new InputBindingKey() { gamepadButton = GamepadButtons.South, text = "A" });
                mapGamepadButtons.Add(GamepadButtons.East, new InputBindingKey() { gamepadButton = GamepadButtons.East, text = "B" });
                mapGamepadButtons.Add(GamepadButtons.West, new InputBindingKey() { gamepadButton = GamepadButtons.West, text = "X" });

                mapGamepadButtons.Add(GamepadButtons.L1, new InputBindingKey() { gamepadButton = GamepadButtons.L1, text = "LB" });
                mapGamepadButtons.Add(GamepadButtons.L2, new InputBindingKey() { gamepadButton = GamepadButtons.L2, text = "LT" });
                mapGamepadButtons.Add(GamepadButtons.R1, new InputBindingKey() { gamepadButton = GamepadButtons.R1, text = "RB" });
                mapGamepadButtons.Add(GamepadButtons.R2, new InputBindingKey() { gamepadButton = GamepadButtons.R2, text = "RT" });
            }
            else
            {
                if (useUnicodeActions)
                {
                    mapGamepadButtons.Add(GamepadButtons.North, new InputBindingKey() { gamepadButton = GamepadButtons.North, text = "" + Convert.ToChar(SeIconChar.Triangle), scaleOverride = scaleOverrideSeIcon });
                    mapGamepadButtons.Add(GamepadButtons.South, new InputBindingKey() { gamepadButton = GamepadButtons.South, text = "" + Convert.ToChar(SeIconChar.Cross), scaleOverride = scaleOverrideSeIcon });
                    mapGamepadButtons.Add(GamepadButtons.East, new InputBindingKey() { gamepadButton = GamepadButtons.East, text = "" + Convert.ToChar(SeIconChar.Circle), scaleOverride = scaleOverrideSeIcon });
                    mapGamepadButtons.Add(GamepadButtons.West, new InputBindingKey() { gamepadButton = GamepadButtons.West, text = "" + Convert.ToChar(SeIconChar.Square), scaleOverride = scaleOverrideSeIcon });
                }
                else
                {
                    mapGamepadButtons.Add(GamepadButtons.North, new InputBindingKey() { gamepadButton = GamepadButtons.North, icon = FontAwesomeIcon.Mountain, scaleOverride = scaleOverrideFontAwesome });
                    mapGamepadButtons.Add(GamepadButtons.South, new InputBindingKey() { gamepadButton = GamepadButtons.South, icon = FontAwesomeIcon.Times, scaleOverride = scaleOverrideFontAwesome });
                    mapGamepadButtons.Add(GamepadButtons.East, new InputBindingKey() { gamepadButton = GamepadButtons.East, icon = FontAwesomeIcon.CircleNotch, scaleOverride = scaleOverrideFontAwesome });
                    mapGamepadButtons.Add(GamepadButtons.West, new InputBindingKey() { gamepadButton = GamepadButtons.West, icon = FontAwesomeIcon.Expand, scaleOverride = scaleOverrideFontAwesome });
                }

                mapGamepadButtons.Add(GamepadButtons.L1, new InputBindingKey() { gamepadButton = GamepadButtons.L1, text = "L1" });
                mapGamepadButtons.Add(GamepadButtons.L2, new InputBindingKey() { gamepadButton = GamepadButtons.L2, text = "L2" });
                mapGamepadButtons.Add(GamepadButtons.R1, new InputBindingKey() { gamepadButton = GamepadButtons.R1, text = "R1" });
                mapGamepadButtons.Add(GamepadButtons.R2, new InputBindingKey() { gamepadButton = GamepadButtons.R2, text = "R2" });
            }

            foreach (var kvp in mapGamepadButtons)
            {
                supportedButtonMask |= kvp.Key;
            }
        }

        private const short GamepadStyleOptionId = 94;
        private const short GamepadStyleValueId = 123;

        /// TEMPORARY, used only for debugging / with outdated ClientStruct 
        [StructLayout(LayoutKind.Explicit, Size = 0xD698)]
        unsafe struct ConfigModuleTesting
        {
            public const int ConfigOptionCount = 680;
            [FieldOffset(0x2C8)] public fixed byte options[Option.Size * ConfigOptionCount];
            [FieldOffset(0xAC18)] public fixed byte values[0x10 * ConfigOptionCount];
        }

        private static unsafe int GetConfigModuleTestingOption(int optionId)
        {
            var configTestPtr = (ConfigModuleTesting*)ConfigModule.Instance();
            var optionsArr = (Option*)configTestPtr->options;
            for (int idx = 0; idx < ConfigModuleTesting.ConfigOptionCount; idx++)
            {
                if ((int)optionsArr[idx].OptionID == optionId)
                {
                    var valuesArr = (AtkValue*)configTestPtr->values;
                    return valuesArr[idx].Int;
                }
            }

            return 0;
        }

        private static unsafe int GetConfigModuleTestingValue(int valueId)
        {
            var configTestPtr = (ConfigModuleTesting*)ConfigModule.Instance();
            var valuesArr = (AtkValue*)configTestPtr->values;

            if (valueId >= 0 && valueId < ConfigModuleTesting.ConfigOptionCount)
            {
                return valuesArr[valueId].Int;
            }

            return 0;
        }

#if DEBUG
        private static unsafe void LogConfigModuleTesting()
        {
            ConfigModule* modulePtr = ConfigModule.Instance();
            if (modulePtr != null)
            {
                var configTestPtr = (ConfigModuleTesting*)modulePtr;
                PluginLog.Log($"module: {(ulong)configTestPtr:X}");

                var optionsArr = (Option*)configTestPtr->options;
                for (int idx = 0; idx < ConfigModuleTesting.ConfigOptionCount; idx++)
                {
                    PluginLog.Log($"option[{idx}] = {(int)optionsArr[idx].OptionID}");
                }

                var valuesArr = (AtkValue*)configTestPtr->values;
                for (int idx = 0; idx < ConfigModuleTesting.ConfigOptionCount; idx++)
                {
                    PluginLog.Log($"value[{idx}] = {valuesArr[idx].Int}");
                }
            }
        }

        public static void TestGamepadStyleSettings()
        {
            PluginLog.Log($"ClientStruct check, num options:{ConfigModule.ConfigOptionCount} (vs {ConfigModuleTesting.ConfigOptionCount})");

            PluginLog.Log("Dumping config data:");
            LogConfigModuleTesting();

            int styleSettings = GetConfigModuleTestingOption(GamepadStyleOptionId);
            PluginLog.Log($"Gamepad style by option (id: {GamepadStyleOptionId}) = {styleSettings}");

            styleSettings = GetConfigModuleTestingValue(GamepadStyleValueId);
            PluginLog.Log($"Gamepad style by value ({GamepadStyleValueId}) = {styleSettings}");
        }
#endif // DEBUG

        private static unsafe int GetGamepadStyleSettings()
        {
            // 6.08 - settings broken, value holding gamepad style is not accessible through optionID
            //          value[123] = style
            //          option[123].optionId = -1 => sadness.
            //
            // TODO: figure it out one day (or wait until it magically fixes itself)
            //
            int settingsValue = GetConfigModuleTestingValue(GamepadStyleValueId);

            /*bool useDefaultConfigs = ConfigModule.ConfigOptionCount >= ConfigModuleTesting.ConfigOptionCount;
            if (useDefaultConfigs)
            {
                ConfigModule* modulePtr = ConfigModule.Instance();
                if (modulePtr != null)
                {
                    var valuePtr = modulePtr->GetValueById(GamepadStyleOptionId);
                    if (valuePtr != null)
                    {
                        settingsValue = valuePtr->Int;
                    }
                }
            }
            else
            {
                settingsValue = GetConfigModuleTestingOption(GamepadStyleOptionId);
            }*/

            return settingsValue;
        }

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        static extern int GetKeyNameText(int lParam, [Out] StringBuilder lpString, int nSize);
    }
}
