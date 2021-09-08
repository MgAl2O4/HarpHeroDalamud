﻿using Dalamud.Game.Gui;
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
            NoErrorsWide,
            AddonNotFound,
            AddonNotVisible,
            NodesNotReady,
        }

        public UIStateBardPerformance cachedState = new();
        public Action<bool> OnVisibilityChanged;

        public Status status = Status.AddonNotFound;
        public bool IsVisible => (status != Status.AddonNotFound) && (status != Status.AddonNotVisible);
        public bool HasErrors => false;

        private GameGui gameGui;
        private IntPtr cachedAddonPtr;

        public UIReaderBardPerformance(GameGui gameGui)
        {
            this.gameGui = gameGui;
        }

        public unsafe void Update()
        {
            IntPtr shortAddonPtr = gameGui.GetAddonByName("PerformanceMode", 1);
            IntPtr wideAddonPtr = IntPtr.Zero;
            IntPtr useAddonPtr = shortAddonPtr;

            if (useAddonPtr == IntPtr.Zero)
            {
                wideAddonPtr = gameGui.GetAddonByName("PerformanceModeWide", 1);
                useAddonPtr = wideAddonPtr;
            }

            if (cachedAddonPtr != useAddonPtr)
            {
                cachedAddonPtr = useAddonPtr;
                cachedState.keys.Clear();
            }

            bool canScanNodes = false;
            if (useAddonPtr == IntPtr.Zero)
            {
#if DEBUG
                bool forceReproStateShort = false;
                bool forceReproStateWide = false;

                if (forceReproStateShort)
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
                else if (forceReproStateWide)
                {
                    cachedState.keys.Clear();
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(1358, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(1304, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(1250, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(1196, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(1142, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(1088, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(1034, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(980, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(1283, 684), size = new Vector2(43, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(1229, 684), size = new Vector2(43, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(1175, 684), size = new Vector2(43, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(1067, 684), size = new Vector2(43, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(1013, 684), size = new Vector2(43, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(926, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(872, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(818, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(764, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(710, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(656, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(602, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(905, 684), size = new Vector2(43, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(851, 684), size = new Vector2(43, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(797, 684), size = new Vector2(43, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(689, 684), size = new Vector2(43, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(635, 684), size = new Vector2(43, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(548, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(494, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(440, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(386, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(332, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(278, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(224, 684), size = new Vector2(55, 167) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(527, 684), size = new Vector2(43, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(473, 684), size = new Vector2(43, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(419, 684), size = new Vector2(43, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(311, 684), size = new Vector2(43, 100) });
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { pos = new Vector2(257, 684), size = new Vector2(43, 100) });

                    SetStatus(Status.NoErrorsWide);
                    return;
                }
#endif // DEBUG

                SetStatus(Status.AddonNotFound);
            }
            else
            {
                var baseNode = (AtkUnitBase*)useAddonPtr;
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

                SetStatus((useAddonPtr == shortAddonPtr) ? Status.NoErrors : Status.NoErrorsWide);
            }
        }

        private unsafe void AddKeyNodeAddresses(AtkResNode* containerNode, int numKeys = 13)
        {
            var nodeArrKeys = GUINodeUtils.GetImmediateChildNodes(containerNode);
            if (nodeArrKeys != null && nodeArrKeys.Length == numKeys)
            {
                foreach (var node in nodeArrKeys)
                {
                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { addr = (ulong)node });
                }
            }
        }

        private unsafe bool FindKeyNodeAddresses(AtkUnitBase* baseNode)
        {
            // root, 7 children (sibling scan)
            //     [1] res node, 13 piano key buttons
            //         [x] Button components
            //
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

            var nodeArrL0 = GUINodeUtils.GetImmediateChildNodes(baseNode->RootNode);
            if (nodeArrL0.Length == 7)
            {
                var nodeA = GUINodeUtils.PickNode(nodeArrL0, 1, 7);
                AddKeyNodeAddresses(nodeA);
            }
            else
            {
                var nodeA = GUINodeUtils.PickNode(nodeArrL0, 1, 9);
                var nodeB = GUINodeUtils.PickNode(nodeArrL0, 2, 9);
                var nodeC = GUINodeUtils.PickNode(nodeArrL0, 3, 9);
                AddKeyNodeAddresses(nodeA);
                AddKeyNodeAddresses(nodeB, 12);
                AddKeyNodeAddresses(nodeC, 12);
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