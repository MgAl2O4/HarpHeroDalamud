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
                cachedState.ResetCachedAddr();
            }

            bool canScanNodes = false;
            if (useAddonPtr == IntPtr.Zero)
            {
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
                        if (!ProcessKeyNodes(baseNode))
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

                    var bindDescNode = (AtkResNode*)keyOb.bindTextAddr;
                    keyOb.bindDesc = GUINodeUtils.GetNodeText(bindDescNode);
                }

                {
                    var bindOctaveUpNode = (AtkResNode*)cachedState.octaveUpAddr;
                    cachedState.octaveUpDesc = GUINodeUtils.GetNodeText(bindOctaveUpNode);

                    var bindOctaveDownNode = (AtkResNode*)cachedState.octaveDownAddr;
                    cachedState.octaveDownDesc = GUINodeUtils.GetNodeText(bindOctaveDownNode);
                }

                CalcKeysBoundingBox();
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
                    // button component, 12 nodes
                    //     [6] component node, 4 children
                    //         [2] text: note name
                    //         [3] text: binding

                    // eh... this is super flaky, investigate reading key bindings directly
                    // short, white key: [6]/12
                    // short, black key: [5]/11
                    // wide,  white key: [6]/8
                    // wide,  black key: [5]/7

                    AtkResNode* nodeA = null;
                    if (node != null && (int)node->Type >= 1000)
                    {
                        var compNode = (AtkComponentNode*)node;
                        var uldManager = compNode->Component->UldManager;
                        nodeA =
                            (uldManager.NodeListCount == 12) ? uldManager.NodeList[6] :
                            (uldManager.NodeListCount == 11) ? uldManager.NodeList[5] :
                            (uldManager.NodeListCount == 8) ? uldManager.NodeList[6] :
                            (uldManager.NodeListCount == 7) ? uldManager.NodeList[5] :
                            null;
                    }

                    var nodeB = GUINodeUtils.PickChildNode(nodeA, 3, 4);

                    cachedState.keys.Add(new UIStateBardPerformance.KeyNode() { addr = (ulong)node, bindTextAddr = (ulong)nodeB });
                }
            }
        }

        private unsafe void AddOctaveKeyNodeAddresses(AtkResNode* containerNode)
        {
            // root, 2 nodes (sibling scan)
            //     [1] list container
            //         [x] 6 list items each with 8 nodes, [0] octave+1, [1] octave-1
            //             [6] key desc

            var nodeArrL0 = GUINodeUtils.GetImmediateChildNodes(containerNode);
            var nodeA = GUINodeUtils.PickNode(nodeArrL0, 1, 2);

            var nodeB1 = GUINodeUtils.PickChildNode(nodeA, 0, 6);
            var nodeC1 = GUINodeUtils.PickChildNode(nodeB1, 6, 8);
            if (nodeC1 == null)
            {
                nodeC1 = GUINodeUtils.PickChildNode(nodeB1, 3, 5);
            }
            cachedState.octaveUpAddr = (ulong)nodeC1;

            var nodeB2 = GUINodeUtils.PickChildNode(nodeA, 1, 6);
            var nodeC2 = GUINodeUtils.PickChildNode(nodeB2, 6, 8);
            if (nodeC2 == null)
            {
                nodeC2 = GUINodeUtils.PickChildNode(nodeB1, 3, 5);
            }
            cachedState.octaveDownAddr = (ulong)nodeC2;
        }

        private unsafe bool ProcessKeyNodes(AtkUnitBase* baseNode)
        {
            // root, 7 children (sibling scan)
            //     [1] res node, 13 piano key buttons
            //         [x] Button components
            //     [2] res node, 2 nodes (sibling scan)
            //         [x] 6 list items each with 8 nodes, [0] octave+1, [1] octave-1
            //             [6] key desc
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

            var nodeArrL0 = GUINodeUtils.GetImmediateChildNodes(baseNode->RootNode);
            if (nodeArrL0.Length == 7)
            {
                var nodeA = GUINodeUtils.PickNode(nodeArrL0, 1, 7);
                AddKeyNodeAddresses(nodeA);

                var nodeD = GUINodeUtils.PickNode(nodeArrL0, 2, 7);
                AddOctaveKeyNodeAddresses(nodeD);
            }
            else
            {
                var nodeA = GUINodeUtils.PickNode(nodeArrL0, 1, 9);
                var nodeB = GUINodeUtils.PickNode(nodeArrL0, 2, 9);
                var nodeC = GUINodeUtils.PickNode(nodeArrL0, 3, 9);
                AddKeyNodeAddresses(nodeA);
                AddKeyNodeAddresses(nodeB, 12);
                AddKeyNodeAddresses(nodeC, 12);

                var nodeD = GUINodeUtils.PickNode(nodeArrL0, 4, 9);
                AddOctaveKeyNodeAddresses(nodeD);
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
    }

    public class UIStateBardPerformance
    {
        public class KeyNode
        {
            public ulong addr;
            public Vector2 pos;
            public Vector2 size;

            public ulong bindTextAddr;
            public string bindDesc;
        }

        public ulong octaveUpAddr;
        public ulong octaveDownAddr;
        public string octaveUpDesc;
        public string octaveDownDesc;

        public Vector2 keysPos;
        public Vector2 keysSize;

        public List<KeyNode> keys = new();

        public void ResetCachedAddr()
        {
            octaveUpAddr = 0;
            octaveDownAddr = 0;
            keys.Clear();
        }
    }
}
