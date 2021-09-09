using Dalamud;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace HarpHero
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "Harp Hero";

        private readonly DalamudPluginInterface pluginInterface;
        private readonly CommandManager commandManager;
        private readonly Framework framework;

        private readonly WindowSystem windowSystem = new("HarpHero");

        private readonly Window statusWindow;
        private readonly CommandInfo statusCommand;

        private readonly UIReaderBardPerformance uiReaderPerformance;
        private readonly TrackAssistant trackAssistant;
        private readonly Localization locManager;

        public static Localization CurrentLocManager;
        private string[] supportedLangCodes = { "en" };

        private List<ITickable> tickableStuff = new List<ITickable>();

        public Plugin(DalamudPluginInterface pluginInterface, Framework framework, CommandManager commandManager, GameGui gameGui)
        {
            this.pluginInterface = pluginInterface;
            this.commandManager = commandManager;
            this.framework = framework;

            // prep utils
            locManager = new Localization("assets/loc", "", true);
            locManager.SetupWithLangCode(pluginInterface.UiLanguage);
            CurrentLocManager = locManager;

            trackAssistant = new TrackAssistant();
            tickableStuff.Add(trackAssistant);

            var fileManager = new MidiFileManager();
            fileManager.OnImported += (_) => trackAssistant.OnTracksImported(fileManager.tracks);

#if DEBUG
            // temp debug stuff
            fileManager.ImportFile(@"D:\temp\test3.mid");
            if (fileManager.tracks.Count > 0)
            {
                trackAssistant.SetTrackSection(0, 10);
                trackAssistant.SetTargetBPM(30);
            }
#endif

            // prep data scrapers
            uiReaderPerformance = new UIReaderBardPerformance(gameGui);

            // prep UI
            statusWindow = new PluginWindowStatus(uiReaderPerformance, trackAssistant, fileManager);
            windowSystem.AddWindow(statusWindow);

            var assistantWindow = new PluginWindowAssistant(uiReaderPerformance, trackAssistant);
            windowSystem.AddWindow(assistantWindow);
            tickableStuff.Add(assistantWindow);

            uiReaderPerformance.OnVisibilityChanged += (active) => statusWindow.IsOpen = active;
            uiReaderPerformance.OnVisibilityChanged += (active) => assistantWindow.OnPerformanceActive(active);
            trackAssistant.OnPlayChanged += (active) => assistantWindow.OnPlayChanged(active);

            // prep plugin hooks
            statusCommand = new(OnCommand);
            commandManager.AddHandler("/harphero", statusCommand);

            pluginInterface.LanguageChanged += OnLanguageChanged;
            pluginInterface.UiBuilder.Draw += OnDraw;

            framework.Update += Framework_OnUpdateEvent;

            // keep at the end to update everything created here
            locManager.LocalizationChanged += (_) => CacheLocalization();
            CacheLocalization();
        }

        private void OnLanguageChanged(string langCode)
        {
            // check if resource is available, will cause exception if trying to load empty json
            if (Array.Find(supportedLangCodes, x => x == langCode) != null)
            {
                locManager.SetupWithLangCode(langCode);
            }
            else
            {
                locManager.SetupWithFallbacks();
            }
        }

        private void CacheLocalization()
        {
            statusCommand.HelpMessage = string.Format(Localization.Localize("Cmd_Status", "Show state of {0} plugin"), Name);
        }

        public void Dispose()
        {
            trackAssistant.Dispose();
            commandManager.RemoveHandler("/harphero");
            windowSystem.RemoveAllWindows();
            framework.Update -= Framework_OnUpdateEvent;
            pluginInterface.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            statusWindow.IsOpen = true;
        }

        private void OnDraw()
        {
            windowSystem.Draw();
        }

        private void Framework_OnUpdateEvent(Framework framework)
        {
            try
            {
                uiReaderPerformance.Update();

                float deltaSeconds = (float)framework.UpdateDelta.TotalSeconds;
                foreach (var tickOb in tickableStuff)
                {
                    tickOb.Tick(deltaSeconds);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "state update failed");
            }
        }
    }
}
