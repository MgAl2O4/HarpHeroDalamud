using Dalamud.Game.Gui;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace HarpHero
{
    public class UIReaderBardPerformance
    {
        public enum Status
        {
            NoErrors,
            AddonNotFound,
            AddonNotVisible,
            NodesNotReady,
            WideMode,
        }

        public UIStateBardPerformance cachedState = new();
        public Action<bool> OnVisibilityChanged;

        public Status status = Status.AddonNotFound;
        public bool IsVisible => (status != Status.AddonNotFound) && (status != Status.AddonNotVisible) && (status != Status.WideMode);
        public bool HasErrors => false;

        private GameGui gameGui;
        private IntPtr cachedAddonPtr;

        public UIReaderBardPerformance(GameGui gameGui)
        {
            this.gameGui = gameGui;
        }

        public unsafe void Update()
        {
            IntPtr addonPtr = gameGui.GetAddonByName("PerformanceMode", 1);
            if (cachedAddonPtr != addonPtr)
            {
                cachedAddonPtr = addonPtr;
                cachedState.keys.Clear();
            }

            bool canScanNodes = false;
            if (addonPtr == IntPtr.Zero)
            {
                bool hasWideMode = false;

                IntPtr wideAddonPtr = gameGui.GetAddonByName("PerformanceModeWide", 1);
                if (wideAddonPtr != IntPtr.Zero)
                {
                    var wideBaseNode = (AtkUnitBase*)wideAddonPtr;
                    if (wideBaseNode->RootNode != null && wideBaseNode->RootNode->IsVisible)
                    {
                        hasWideMode = true;
                    }
                }

#if DEBUG
                bool forceReproState = false;
                if (forceReproState)
                {
                    cachedState.keys.Clear();
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(1037, 684), size = new Vector2(63, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(975, 684), size = new Vector2(63, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(913, 684), size = new Vector2(63, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(851, 684), size = new Vector2(63, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(789, 684), size = new Vector2(63, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(727, 684), size = new Vector2(63, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(665, 684), size = new Vector2(63, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(603, 684), size = new Vector2(63, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(951, 684), size = new Vector2(49, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(889, 684), size = new Vector2(49, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(827, 684), size = new Vector2(49, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(703, 684), size = new Vector2(49, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(641, 684), size = new Vector2(49, 100) });
                    SetStatus(Status.NoErrors);
                    return;
                }
#endif // DEBUG

                SetStatus(hasWideMode ? Status.WideMode : Status.AddonNotFound);
            }
            else
            {
                var baseNode = (AtkUnitBase*)addonPtr;
                if (baseNode->RootNode == null || !baseNode->RootNode->IsVisible)
                {
                    SetStatus(Status.AddonNotVisible);
                }
                else
                {
                    if (cachedState.keys.Count == 0)
                    {
                        if (!FindKeyNodeAddresses(baseNode))
                        {
                            SetStatus(Status.NodesNotReady);
                        }
                    }

                    canScanNodes = cachedState.keys.Count > 0;
                }
            }

            if (canScanNodes)
            {
                foreach (var keyOb in cachedState.keys)
                {
                    var keyNode = (AtkResNode*)keyOb.addr;
                    (keyOb.pos, keyOb.size) = GUINodeUtils.GetNodePosAndSize(keyNode);
                }

                SetStatus(Status.NoErrors);
            }
        }

        private unsafe bool FindKeyNodeAddresses(AtkUnitBase* baseNode)
        {
            // root, 7 children (sibling scan)
            //     [1] res node, 13 piano key buttons
            //         [x] Button components

            var nodeArrL0 = GUINodeUtils.GetImmediateChildNodes(baseNode->RootNode);
            var nodeA = GUINodeUtils.PickNode(nodeArrL0, 1, 7);
            var nodeArrKeys = GUINodeUtils.GetImmediateChildNodes(nodeA);
            if (nodeArrKeys != null && nodeArrKeys.Length == 13)
            {
                foreach (var nodeB in nodeArrKeys)
                {
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { addr = (ulong)nodeB });
                }
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
                    PluginLog.Error("Performance reader error: " + newStatus);
                }

                if (wasVisible != IsVisible)
                {
                    OnVisibilityChanged?.Invoke(IsVisible);
                }
            }
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

        public List<KeyNode> keys = new();
    }
}
