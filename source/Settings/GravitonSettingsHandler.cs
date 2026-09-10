using CommunityToolkit.Mvvm.ComponentModel;

using Emunight;

using Graviton.Models.Notifications;
using Graviton.Notifications;

using Playnite;

using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows.Controls;

namespace Graviton.Settings
{
    [INotifyPropertyChanged]
    public partial class GravitonSettingsHandler : PluginSettingsHandler
    {
        public bool InEditingMode { get; private set; }

        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private static GravitonLogger? _logger;
        private IRomMServer _romMServer;

        [ObservableProperty] private GravitonPluginSettings settings = new();

        public GravitonSettingsHandler(GravitonPlugin plugin, IPlayniteApi playniteAPI, GravitonLogger logger, IRomMServer server) 
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
            _romMServer = server;
        }

        public override UserControl GetEditView(GetSettingsViewArgs args)
        {
            return new GravitonSettingsView { DataContext = this };
        }

        public override async Task BeginEditAsync(BeginEditArgs args)
        {
            Settings = _plugin.Settings.Clone();
            InEditingMode = true;

            foreach (var mapping in Settings.Mappings)
            {
                mapping.AvailablePlatforms = Settings.RomMPlatforms.Where(x => x.RomCount > 0).ToObservableCollection();
                _logger?.Trace($"Updated availiable platforms");
                mapping.AvailableEmulators = ((IEnumerable<EmulatorBase>)GravitonPlugin.Instance.EmunightAPI!.ImportedEmulators).Concat(GravitonPlugin.Instance.EmunightAPI.CustomEmulators).OrderBy(e => e.Name).ToObservableCollection();
                _logger?.Trace($"Updated availiable emulators");
            }

            _logger?.Trace($"Began settings edit");
            await Task.CompletedTask;
        }

        public override async Task CancelEditAsync(CancelEditArgs args)
        {
            InEditingMode = false;
            // Remove editing copy headers
            foreach (var header in Settings.CustomHeaders)
            {
                if (string.IsNullOrEmpty(header.Name))
                    continue;

                _romMServer.RemoveHeader(header.Name);
            }
            _logger?.Trace($"Removed edited headers");

            // Add old headers back
            foreach (var header in _plugin.Settings.CustomHeaders)
            {
                if (string.IsNullOrEmpty(header.Name) || string.IsNullOrEmpty(header.Value) || !header.Enabled)
                    continue;

                _romMServer.AddHeader(header.Name, header.Value);
            }
            _logger?.Trace($"Restored old headers");

            _logger?.Trace($"Cancelled settings edit");
            await Task.CompletedTask;
        }

        public override async Task EndEditAsync(EndEditArgs args)
        {
            _logger?.Trace($"Ended settings edit");

            _plugin.Settings = Settings;
            SaveSettings(_playniteAPI.UserDataDir, Settings);
            foreach (var header in Settings.CustomHeaders.Where(x => x.Enabled))
            {
                _romMServer.RemoveHeader(header.Name);
                _romMServer.AddHeader(header.Name, header.Value);
            }
            InEditingMode = false;
            await Task.CompletedTask;
        }

        public override async Task<ICollection<string>> VerifySettingsAsync(VerifySettingsArgs args)
        {
            _logger?.Trace($"Verified settings");
            await Task.CompletedTask;
            return [];
        }

        public static void SaveSettings(string dataDir, GravitonPluginSettings settings)
        {
            var setFile = Path.Combine(dataDir, "settings.json");
            try
            {
                File.WriteAllText(setFile, JsonSerializer.Serialize<GravitonPluginSettings>(settings, new JsonSerializerOptions { WriteIndented = true }));
                _logger?.Trace($"Saved settings to {setFile}");
            }
            catch (Exception ex)
            {
                GravitonNotify.Notify("graviton.settings.save.failed", Loc.GetString("SettingSaveFailed", ("Error", ex.Message)), GravitonSeverity.Error, ex);
            }
        }

        public static GravitonPluginSettings LoadSettings(string dataDir)
        {
            GravitonPluginSettings? settings = null;
            var setFile = Path.Combine(dataDir, "settings.json");
            if (File.Exists(setFile))
            {
                try
                {
                    var file = File.ReadAllText(setFile);
                    _logger?.Trace($"Red settings.json");

                    settings = JsonSerializer.Deserialize<GravitonPluginSettings>(file);
                    if (settings != null)
                    {
                        foreach (var mapping in settings.Mappings)
                        {
                            mapping.AvailablePlatforms = settings.RomMPlatforms.Where(x => x.RomCount > 0).ToObservableCollection();
                            _logger?.Trace($"Restored Available Platforms");
                            mapping.AvailableEmulators = ((IEnumerable<EmulatorBase>)GravitonPlugin.Instance.EmunightAPI!.ImportedEmulators).Concat(GravitonPlugin.Instance.EmunightAPI.CustomEmulators).OrderBy(e => e.Name).ToObservableCollection();
                            _logger?.Trace($"Restored Available Emulators");
                        }
                    }
                }
                catch (Exception ex)
                {
                    GravitonNotify.Notify("graviton.settings.load.failed", Loc.GetString("SettingLoadFailed", ("Error", ex.Message)), GravitonSeverity.Error, ex);
                }
            }

            if (settings is null)
            {
                _logger?.Trace($"No settings.json file found, creating new settings");
                return new GravitonPluginSettings();
            }
            else
            {
                _logger?.Trace($"Loaded settings");
                return settings;
            }
        }       
    }
}