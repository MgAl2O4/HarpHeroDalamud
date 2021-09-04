using Dalamud.Game.Gui;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace TriadBuddyPlugin
{
    public class UIReaderBardPerformance
    {
        public enum Status
        {
            NoErrors,
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
            IntPtr addonPtr = gameGui.GetAddonByName("GSInfoCardList", 1);
            if (cachedAddonPtr != addonPtr)
            {
                cachedAddonPtr = addonPtr;
                cachedState.pianoKeyNodeAddr.Clear();
            }

            if (addonPtr == IntPtr.Zero)
            {
                SetStatus(Status.AddonNotFound);
                return;
            }

            var baseNode = (AtkUnitBase*)addonPtr;
            if (baseNode != null && baseNode->RootNode != null && baseNode->RootNode->IsVisible)
            {
                SetStatus(Status.AddonNotVisible);
                return;
            }

            /*
            if (cachedState.descNodeAddr == 0)
            {
                if (!FindTextNodeAddresses(addon))
                {
                    SetStatus(Status.NodesNotReady);
                    return;
                }
            }

            var descNode = (AtkResNode*)cachedState.descNodeAddr;
            (cachedState.screenPos, cachedState.screenSize) = GUINodeUtils.GetNodePosAndSize(addon->AtkUnitBase.RootNode);
            (cachedState.descriptionPos, cachedState.descriptionSize) = GUINodeUtils.GetNodePosAndSize(descNode);

            var addonAgent = (AgentTriadCardList*)cachedAddonAgentPtr;
            if (cachedState.pageIndex != addon->PageIndex ||
                cachedState.cardIndex != addon->CardIndex ||
                cachedState.filterMode != addonAgent->FilterMode ||
                cachedState.numU != addon->NumSideU)
            {
                cachedState.numU = addon->NumSideU;
                cachedState.numL = addon->NumSideL;
                cachedState.numD = addon->NumSideD;
                cachedState.numR = addon->NumSideR;
                cachedState.rarity = addon->CardRarity;
                cachedState.type = addon->CardType;
                cachedState.iconId = addon->CardIconId;
                cachedState.pageIndex = addon->PageIndex;
                cachedState.cardIndex = addon->CardIndex;
                cachedState.filterMode = addonAgent->FilterMode;

                OnUIStateChanged?.Invoke(cachedState);
            }*/

            SetStatus(Status.NoErrors);
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
        public List<ulong> pianoKeyNodeAddr = new();
        public List<Tuple<Vector2, Vector2>> pianoKeyPosAndSize = new();
    }
}
