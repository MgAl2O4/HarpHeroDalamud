using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Component.GUI;
using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace HarpHero
{
    public class UIReaderBardPerformance
    {
        public enum Status
        {
            NoErrors,
            NoErrorsWide,
            AddonNotFound,
            AddonNotVisible,
            NodesNotReady,
        }

        [StructLayout(LayoutKind.Explicit, Size = 0x64)]
        public unsafe struct AgentData
        {
            [FieldOffset(0x5c)] public int NoteNumberOffset;
            [FieldOffset(0x60)] public int ActiveNoteNumber;
            // same agent used for both addon modes
        }

        public UIStateBardPerformance cachedState = new();
        public Action<bool>? OnVisibilityChanged;
        public Action<int>? OnOctaveOffsetChanged;
        public Action<int>? OnPlayingNoteChanged;
        public Action<bool>? OnKeyboardModeChanged;
        public Action<int>? OnCachedKeysChanged;

        public Status status = Status.AddonNotFound;
        public bool IsVisible => (status != Status.AddonNotFound) && (status != Status.AddonNotVisible);
        public bool HasErrors => false;

        private IntPtr cachedAgentPtr;
        private int cachedNumKeys;
        private bool isWideMode;

        public UIReaderBardPerformanceShort uiReaderShort = new();
        public UIReaderBardPerformanceWide uiReaderWide = new();

        public UIReaderBardPerformance()
        {
            uiReaderShort.parentReader = this;
            uiReaderWide.parentReader = this;

            Plugin.OnDebugSnapshot += (_) =>
            {
                Service.logger.Info($"UIReaderBardPerformance: status:{status}, numKeys:{cachedNumKeys}, isWide:{isWideMode}, useGamepad:{cachedState.isGamepad}");
            };
        }

        public void OnAddonLost()
        {
            cachedAgentPtr = IntPtr.Zero;
            cachedState.ResetCachedAddr();

            SetStatus(Status.AddonNotFound);

            if (cachedNumKeys != cachedState.keys.Count)
            {
                SetCachedNumKeys(cachedState.keys.Count);
            }
        }

        public void OnAddonShown(IntPtr addonPtr, bool isWide)
        {
            cachedAgentPtr = Service.gameGui.FindAgentInterface(addonPtr);
            isWideMode = isWide;
        }

        public void ApplyTestSetup()
        {
            cachedState.keysPos = new Vector2(100, 300);
            cachedState.keysSize = new Vector2(800, 200);

            int numKeys = 12 + 12 + 12 + 1;
            for (int idx = 0; idx < numKeys; idx++)
            {
                var keyData = new UIStateBardPerformance.KeyNode();
                keyData.pos = new Vector2(100 + (idx * 50), 300);
                keyData.size = new Vector2(50, 100);
                cachedState.keys.Add(keyData);
            }

            SetStatus(Status.NoErrorsWide);
            SetCachedNumKeys(numKeys);
        }

        public unsafe void OnAddonUpdate(IntPtr addonPtr)
        {
            if (cachedState.keys.Count == 0)
            {
                if (!ProcessKeyNodes((AtkUnitBase*)addonPtr))
                {
                    SetStatus(Status.NodesNotReady);
                }
            }

            bool canScanNodes = cachedState.keys.Count > 0;
            if (canScanNodes)
            {
                foreach (var keyOb in cachedState.keys)
                {
                    var keyNode = (AtkResNode*)keyOb.addr;
                    (keyOb.pos, keyOb.size) = GUINodeUtils.GetNodePosAndSize(keyNode);
                }

                if (cachedState.footerDescriptionAddr != 0)
                {
                    var footerText = GUINodeUtils.GetNodeText((AtkResNode*)cachedState.footerDescriptionAddr) ?? "";
                    if (cachedState.footerDescText != footerText)
                    {
                        cachedState.footerDescText = footerText;

                        bool hasGamepadIcon = HasGamepadIcon(footerText);
                        if (cachedState.isGamepad != hasGamepadIcon)
                        {
                            cachedState.isGamepad = hasGamepadIcon;
                            OnKeyboardModeChanged?.Invoke(!hasGamepadIcon);
                        }
                    }
                }

                CalcKeysBoundingBox();
                SetStatus(isWideMode ? Status.NoErrorsWide : Status.NoErrors);
            }

            UpdatePlayingNote();

            if (cachedNumKeys != cachedState.keys.Count)
            {
                SetCachedNumKeys(cachedState.keys.Count);
            }
        }

        private void SetCachedNumKeys(int numKeys)
        {
            cachedNumKeys = numKeys;
            OnCachedKeysChanged?.Invoke(numKeys);
        }

        private string? gamepadIconString;
        private bool HasGamepadIcon(string text)
        {
            if (gamepadIconString == null)
            {
                // look for L3 icon
                var iconPayload = new IconPayload(BitmapFontIcon.ControllerAnalogLeftStickIn);
                var iconBytes = iconPayload.Encode(true);

                gamepadIconString = Encoding.ASCII.GetString(iconBytes);
            }

            return text.Contains(gamepadIconString);
        }

        private unsafe void AddKeyNodeAddresses(AtkResNode* containerNode, int numKeys = 13)
        {
            var nodeArrKeys = GUINodeUtils.GetImmediateChildNodes(containerNode);
            if (nodeArrKeys != null && nodeArrKeys.Length == numKeys)
            {
                foreach (var node in nodeArrKeys)
                {
                    // button component, 12 nodes
                    //     [6] component node, 4 children
                    //         [2] text: note name
                    //         [3] text: binding

                    // this is super flaky, use real bindings instead
                    // short, white key: [6]/12
                    // short, black key: [5]/11
                    // wide,  white key: [6]/8
                    // wide,  black key: [5]/7

                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { addr = (ulong)node });
                }
            }
        }

        private unsafe bool ProcessKeyNodes(AtkUnitBase* baseNode)
        {
            // root, 7 children (sibling scan)
            //     [1] res node, 13 piano key buttons
            //         [x] Button components
            //     [2] res node, 2 nodes (sibling scan)
            //         [x] 6 list items each with 8 nodes, [0] octave+1, [1] octave-1
            //             [6] key desc
            //     [6] TextNineGrid node, 2 nodes on list
            //         [1] text => check if starts with icon (gamepad) or not (keyboard)
            //
            // wide mode is very similar, with 3 containers for each octave
            //
            // root, 9 children (sibling scan)
            //     [1] res node, 13 piano key buttons
            //         [x] Button components
            //     [2] res node, 12 piano key buttons
            //         [x] Button components
            //     [3] res node, 12 piano key buttons
            //         [x] Button components
            //     [4] key maps as above
            //     [7] TextNineGrid node, 2 nodes on list
            //         [1] text => check if starts with icon (gamepad) or not (keyboard)

            var nodeArrL0 = GUINodeUtils.GetImmediateChildNodes(baseNode->RootNode);
            if (nodeArrL0 != null && nodeArrL0.Length == 7)
            {
                var nodeA = GUINodeUtils.PickNode(nodeArrL0, 1, 7);
                AddKeyNodeAddresses(nodeA);

                var nodeI1 = GUINodeUtils.PickNode(nodeArrL0, 6, 7);
                var nodeI2 = GUINodeUtils.PickChildNode(nodeI1, 1, 2);
                cachedState.footerDescriptionAddr = (long)nodeI2;
            }
            else
            {
                var nodeA = GUINodeUtils.PickNode(nodeArrL0, 1, 9);
                var nodeB = GUINodeUtils.PickNode(nodeArrL0, 2, 9);
                var nodeC = GUINodeUtils.PickNode(nodeArrL0, 3, 9);
                AddKeyNodeAddresses(nodeA);
                AddKeyNodeAddresses(nodeB, 12);
                AddKeyNodeAddresses(nodeC, 12);

                var nodeI1 = GUINodeUtils.PickNode(nodeArrL0, 7, 9);
                var nodeI2 = GUINodeUtils.PickChildNode(nodeI1, 1, 2);
                cachedState.footerDescriptionAddr = (long)nodeI2;
            }

            return cachedState.keys.Count > 0;
        }

        private void SetStatus(Status newStatus)
        {
            if (status != newStatus)
            {
                bool wasVisible = IsVisible;
                status = newStatus;

                if (HasErrors)
                {
                    Service.logger.Error("Performance reader error: " + newStatus);
                }

                if (wasVisible != IsVisible)
                {
                    OnVisibilityChanged?.Invoke(IsVisible);
                }
            }
        }

        private void CalcKeysBoundingBox()
        {
            if (cachedState.keys.Count > 0)
            {
                var key0 = cachedState.keys[0];
                float bbMinX = key0.pos.X;
                float bbMinY = key0.pos.Y;
                float bbMaxX = key0.pos.X + key0.size.X;
                float bbMaxY = key0.pos.Y + key0.size.Y;

                for (int idx = 1; idx < cachedState.keys.Count; idx++)
                {
                    var keyIt = cachedState.keys[idx];

                    bbMinX = Math.Min(bbMinX, keyIt.pos.X);
                    bbMinY = Math.Min(bbMinY, keyIt.pos.Y);
                    bbMaxX = Math.Max(bbMaxX, keyIt.pos.X + keyIt.size.X);
                    bbMaxY = Math.Max(bbMaxY, keyIt.pos.Y + keyIt.size.Y);
                }

                cachedState.keysPos = new Vector2(bbMinX, bbMinY);
                cachedState.keysSize = new Vector2(bbMaxX - bbMinX, bbMaxY - bbMinY);
            }
            else
            {
                cachedState.keysPos = Vector2.Zero;
                cachedState.keysSize = Vector2.Zero;
            }
        }

        private unsafe void UpdatePlayingNote()
        {
            int newNoteIdx = 0;
            int newNoteOffset = 0;

            var agentDataPtr = (AgentData*)cachedAgentPtr;
            if (agentDataPtr != null)
            {
                newNoteIdx = agentDataPtr->ActiveNoteNumber;
                newNoteOffset = agentDataPtr->NoteNumberOffset;

                bool isNewValid = newNoteIdx > 0 && newNoteIdx < (12 * 8);
                if (!isNewValid)
                {
                    newNoteIdx = 0;
                }
            }

            if (cachedState.activeNoteOffset != newNoteOffset)
            {
                cachedState.activeNoteOffset = newNoteOffset;
                OnOctaveOffsetChanged?.Invoke(cachedState.ActiveOctaveOffset);
            }

            if (cachedState.activeNote != newNoteIdx)
            {
                cachedState.activeNote = newNoteIdx;
                OnPlayingNoteChanged?.Invoke(cachedState.ActiveNoteNumber);
            }
        }
    }

    // helper class for scheduler: handles single octave performance UI and passes all notifies to parent
    public class UIReaderBardPerformanceShort : IUIReader
    {
        public UIReaderBardPerformance? parentReader;

        public string GetAddonName()
        {
            return "PerformanceMode";
        }

        public void OnAddonLost()
        {
            parentReader?.OnAddonLost();
        }

        public void OnAddonShown(IntPtr addonPtr)
        {
            parentReader?.OnAddonShown(addonPtr, false);
        }

        public void OnAddonUpdate(IntPtr addonPtr)
        {
            parentReader?.OnAddonUpdate(addonPtr);
        }
    }

    // helper class for scheduler: handles three octaves performance UI and passes all notifies to parent
    public class UIReaderBardPerformanceWide : IUIReader
    {
        public UIReaderBardPerformance? parentReader;

        public string GetAddonName()
        {
            return "PerformanceModeWide";
        }

        public void OnAddonLost()
        {
            parentReader?.OnAddonLost();
        }

        public void OnAddonShown(IntPtr addonPtr)
        {
            parentReader?.OnAddonShown(addonPtr, true);
        }

        public void OnAddonUpdate(IntPtr addonPtr)
        {
            parentReader?.OnAddonUpdate(addonPtr);
        }
    }

    public class UIStateBardPerformance
    {
        public class KeyNode
        {
            public ulong addr;
            public Vector2 pos;
            public Vector2 size;
        }

        public Vector2 keysPos;
        public Vector2 keysSize;

        public long footerDescriptionAddr;
        public string? footerDescText;
        public bool isGamepad;

        public int activeNote;
        public int activeNoteOffset;

        public int ActiveNoteNumber => (activeNote > 10) ? (activeNote + 9) : 0;
        public int ActiveOctaveOffset => activeNoteOffset / 12;

        public List<KeyNode> keys = new();

        public void ResetCachedAddr()
        {
            keys.Clear();
            footerDescriptionAddr = 0;
        }
    }
}
