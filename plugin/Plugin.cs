using Dalamud;
using Dalamud.Game;
using Dalamud.Game.ClientState.Keys;
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

        private readonly PluginWindowStatus statusWindow;
        private readonly CommandInfo statusCommand;

        private readonly UIReaderBardPerformance uiReaderPerformance;
        private readonly UnsafeReaderPerformanceKeybinds keybindReader;
        private readonly TrackAssistant trackAssistant;
        private readonly UnsafeMetronomeLink metronome;
        private readonly Localization locManager;

        public static Localization CurrentLocManager;
        private string[] supportedLangCodes = { "en" };

        private List<ITickable> tickableStuff = new List<ITickable>();
        private Configuration configuration { get; init; }

        public Plugin(DalamudPluginInterface pluginInterface, Framework framework, CommandManager commandManager, GameGui gameGui, SigScanner sigScanner, KeyState keyState)
        {
            this.pluginInterface = pluginInterface;
            this.commandManager = commandManager;
            this.framework = framework;

            configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(pluginInterface);

            // prep utils
            locManager = new Localization("assets/loc", "", true);
            locManager.SetupWithLangCode(pluginInterface.UiLanguage);
            CurrentLocManager = locManager;

            var noteMapper = new NoteUIMapper();
            var noteInputWatch = new NoteInputWatcher(noteMapper, keyState);

            // prep data scrapers
            uiReaderPerformance = new UIReaderBardPerformance(gameGui);
            keybindReader = new UnsafeReaderPerformanceKeybinds(gameGui, sigScanner);
            metronome = new UnsafeMetronomeLink(gameGui, sigScanner);

            // more utils
            trackAssistant = new TrackAssistant(metronome, noteInputWatch, configuration);
            trackAssistant.OnTrackChanged += (valid) => noteMapper.OnTrackChanged(trackAssistant);
            tickableStuff.Add(trackAssistant);

            var fileManager = new MidiFileManager();
            fileManager.OnImported += (_) => { trackAssistant.OnTracksImported(fileManager.tracks); };

#if DEBUG
            // temp debug stuff
            fileManager.ImportFile(@"D:\temp\test3.mid");
#endif

            // prep UI
            statusWindow = new PluginWindowStatus(trackAssistant, fileManager, configuration);
            windowSystem.AddWindow(statusWindow);

            var noteAssistantWindow = new PluginWindowNoteAssistant(uiReaderPerformance, trackAssistant, noteMapper, noteInputWatch);
            windowSystem.AddWindow(noteAssistantWindow);
            tickableStuff.Add(noteAssistantWindow);

            var bindAssistantWindow = new PluginWindowBindAssistant(uiReaderPerformance, trackAssistant, noteMapper, noteInputWatch);
            windowSystem.AddWindow(bindAssistantWindow);
            tickableStuff.Add(bindAssistantWindow);

            uiReaderPerformance.OnVisibilityChanged += (active) => statusWindow.IsOpen = active;
            uiReaderPerformance.OnVisibilityChanged += (active) => { noteAssistantWindow.OnPerformanceActive(active); bindAssistantWindow.OnPerformanceActive(active); };
            trackAssistant.OnPlayChanged += (active) =>
            {
                noteInputWatch.OnKeyBindsSet(keybindReader.ReadBindings());
                noteAssistantWindow.OnPlayChanged(active);
                bindAssistantWindow.OnPlayChanged(active);
            };

            // prep plugin hooks
            statusCommand = new(OnCommand);
            commandManager.AddHandler("/harphero", statusCommand);

            pluginInterface.LanguageChanged += OnLanguageChanged;
            pluginInterface.UiBuilder.Draw += OnDraw;
            pluginInterface.UiBuilder.OpenConfigUi += OnOpenConfig;

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

        private void OnOpenConfig()
        {
            statusWindow.showConfigs = true;
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
                metronome.Update();

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
