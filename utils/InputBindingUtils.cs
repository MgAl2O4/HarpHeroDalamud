using Dalamud.Game.ClientState.GamePad;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Text;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace HarpHero
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
        public readonly string simpleText;
        public readonly string separator;

        public InputBindingChord(InputBindingKey[] keys, string separator = " + ")
        {
            this.keys = keys;
            this.separator = separator;
            simpleText = InputBindingUtils.TryCreatingSimpleChordDescription(keys, separator, ignoreScaleOverrides: false);
        }
    }

    public class InputBindingUtils
    {
        private static Dictionary<VirtualKey, InputBindingKey> mapVirtualKeys = new();
        private static Dictionary<GamepadButtons, InputBindingKey> mapGamepadButtons = new();
        private static GamepadButtons supportedGamepadButtons = 0;
        private static bool isUsingXboxGamepadStyle = true;

        public static bool IsUsingXboxGamepadStyle => isUsingXboxGamepadStyle;

        public static InputBindingKey GetVirtualKeyData(VirtualKey key)
        {
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
            if ((supportedGamepadButtons & button) == 0)
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

        private static void InitializeGamepadMap()
        {
            // font is kind of small, but still better than using mountains for triangle :<
            bool useUnicodeSouth = true;

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
                if (useUnicodeSouth)
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
                supportedGamepadButtons |= kvp.Key;
            }
        }

        private static unsafe int GetGamepadStyleSettings()
        {
            // magic number being magical.
            const short gamepadStyleOption = 91;

            ConfigModule* modulePtr = ConfigModule.Instance();
            if (modulePtr != null)
            {
                var valuePtr = modulePtr->GetValueById(gamepadStyleOption);
                if (valuePtr != null)
                {
                    return (int)valuePtr->Value;
                }
            }

            return 0;
        }

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        static extern int GetKeyNameText(int lParam, [Out] StringBuilder lpString, int nSize);
    }
}
