using Dalamud;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Gui.Toast;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using MgAl2O4.Utils;
using System;

namespace HarpHero
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "Harp Hero";

        private readonly WindowSystem windowSystem = new("HarpHero");

        private readonly PluginWindowStatus statusWindow;
        private readonly CommandInfo statusCommand;

        private readonly UIReaderBardPerformance uiReaderPerformance;
        private readonly UnsafeReaderPerformanceKeybinds keybindReader;
        private readonly UnsafeMetronomeLink metronome;
        private readonly UnsafePerformanceHook performanceHook;
        private readonly NoteUIMapper noteUiMapper;
        private readonly Localization locManager;

        private readonly UIReaderScheduler uiReaderScheduler;
        public static readonly TickScheduler TickScheduler = new();

        public static Action<int> OnDebugSnapshot;
        public static Localization CurrentLocManager;
        private string[] supportedLangCodes = { "en" };

        public Plugin(DalamudPluginInterface pluginInterface)
        {
            pluginInterface.Create<Service>();

            Service.plugin = this;

            Service.config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Service.config.Initialize(pluginInterface);

            var myAssemblyName = GetType().Assembly.GetName().Name;
            locManager = new Localization($"{myAssemblyName}.assets.loc.", "", true);            // res stream format: HarpHero.assets.loc.en.json
            locManager.SetupWithLangCode(pluginInterface.UiLanguage);
            CurrentLocManager = locManager;

            // prep data scrapers
            uiReaderPerformance = new UIReaderBardPerformance();
            keybindReader = new UnsafeReaderPerformanceKeybinds();
            metronome = new UnsafeMetronomeLink();
            performanceHook = new UnsafePerformanceHook();

            uiReaderScheduler = new UIReaderScheduler(Service.gameGui);
            uiReaderScheduler.AddObservedAddon(uiReaderPerformance.uiReaderShort);
            uiReaderScheduler.AddObservedAddon(uiReaderPerformance.uiReaderWide);
            uiReaderScheduler.AddObservedAddon(metronome.uiReader);

            // prep utils
            noteUiMapper = new NoteUIMapper();
            var noteInputMapper = new NoteInputMapper(noteUiMapper, keybindReader);

            Service.trackAssistant = new TrackAssistant(uiReaderPerformance, metronome, performanceHook);

            var fileManager = new MidiFileManager();
            fileManager.OnImported += (_) => { Service.trackAssistant.OnTracksImported(fileManager.tracks); };

            var trackHealthCheck = new TrackHealthCheck(noteInputMapper, Service.trackAssistant, uiReaderPerformance);

            // prep UI
            statusWindow = new PluginWindowStatus(fileManager, trackHealthCheck);
            var trackViewWindow = new PluginWindowTrackView();
            var noteAssistantWindow = new PluginWindowNoteAssistant(uiReaderPerformance, noteUiMapper, noteInputMapper);
            var bindAssistantWindow = new PluginWindowBindAssistant(uiReaderPerformance, noteUiMapper, noteInputMapper);
            var noteAssistant2Window = new PluginWindowNoteAssistant2(uiReaderPerformance, noteUiMapper);
            var scoreWindow = new PluginWindowScore(uiReaderPerformance);

            statusWindow.OnShowTrack += (track) => trackViewWindow.OnShowTrack(track);
            uiReaderPerformance.OnVisibilityChanged += (active) => statusWindow.OnPerformanceVisibilityChanged(active);
            uiReaderPerformance.OnKeyboardModeChanged += (isKeyboard) => noteInputMapper.OnKeyboardModeChanged(isKeyboard);
            uiReaderPerformance.OnCachedKeysChanged += (_) => noteUiMapper.OnNumKeysChanged(uiReaderPerformance.cachedState);
            Service.trackAssistant.OnTrackChanged += (valid) => noteUiMapper.OnTrackChanged(Service.trackAssistant);
            Service.trackAssistant.OnPlayChanged += (active) => noteInputMapper.OnPlayChanged(active);
            Service.trackAssistant.OnPerformanceScore += (accuracy) =>
            {
                if (Service.config.ShowScore)
                {
                    var accuracyToastOptions = new QuestToastOptions() { Position = QuestToastPosition.Centre, DisplayCheckmark = true, IconId = 0, PlaySound = true };
                    Service.toastGui.ShowQuest(string.Format(Localization.Localize("Toast_PerformanceAccuracy", "Accuracy: {0:P0}"), accuracy), accuracyToastOptions);
                }
            };

            //uiReaderPerformance.ApplyTestSetup();
            //InputBindingUtils.TestGamepadStyleSettings();

            windowSystem.AddWindow(statusWindow);
            windowSystem.AddWindow(trackViewWindow);
            windowSystem.AddWindow(noteAssistantWindow);
            windowSystem.AddWindow(bindAssistantWindow);
            windowSystem.AddWindow(noteAssistant2Window);
            windowSystem.AddWindow(scoreWindow);

            // prep plugin hooks
            statusCommand = new(OnCommand);
            Service.commandManager.AddHandler("/harphero", statusCommand);

            pluginInterface.LanguageChanged += OnLanguageChanged;
            pluginInterface.UiBuilder.Draw += OnDraw;
            pluginInterface.UiBuilder.OpenConfigUi += OnOpenConfig;

            Service.framework.Update += Framework_OnUpdateEvent;

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
            performanceHook.Dispose();
            Service.trackAssistant.Dispose();
            Service.commandManager.RemoveHandler("/harphero");
            windowSystem.RemoveAllWindows();
            Service.framework.Update -= Framework_OnUpdateEvent;
        }

        private static int debugSnapshotCounter = 0;
        public static void RequestDebugSnapshot()
        {
            PluginLog.Log($"Requesting debug snapshot #{debugSnapshotCounter}");
            OnDebugSnapshot?.Invoke(debugSnapshotCounter);
            debugSnapshotCounter++;
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
                float deltaSeconds = (float)framework.UpdateDelta.TotalSeconds;
                uiReaderScheduler.Update(deltaSeconds);
                TickScheduler.Update(deltaSeconds);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "state update failed");
            }
        }
    }
}
