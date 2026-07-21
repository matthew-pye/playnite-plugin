using CommunityToolkit.Mvvm.ComponentModel;

using Graviton.Models.Notifications;

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
        private ILogger _logger;

        [ObservableProperty] private GravitonPluginSettings settings = new();

        public GravitonSettingsHandler(GravitonPlugin plugin, IPlayniteApi playniteAPI, ILogger logger) 
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;   
        }

        public override UserControl GetEditView(GetSettingsViewArgs args)
        {
            return new GravitonSettingsView { DataContext = this };
        }

        public override async Task BeginEditAsync(BeginEditArgs args)
        {
            Settings = _plugin.Settings.Clone();
            InEditingMode = true;
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

                HttpClientSingleton.Instance.DefaultRequestHeaders.Remove(header.Name);
            }

            // Add old headers back
            foreach (var header in _plugin.Settings.CustomHeaders)
            {
                if (string.IsNullOrEmpty(header.Name) || string.IsNullOrEmpty(header.Value))
                    continue;

                HttpClientSingleton.Instance.DefaultRequestHeaders.Add(header.Name, header.Value);
            }

            await Task.CompletedTask;
        }

        public override async Task EndEditAsync(EndEditArgs args)
        {
            _plugin.Settings = Settings;
            SaveSettings(_playniteAPI.UserDataDir, Settings);
            foreach (var header in Settings.CustomHeaders.Where(x => x.Enabled))
            {
                HttpClientSingleton.Instance.DefaultRequestHeaders.Remove(header.Name);
                HttpClientSingleton.Instance.DefaultRequestHeaders.Add(header.Name, header.Value);
            }
            InEditingMode = false;

            await Task.CompletedTask;
        }

        public override async Task<ICollection<string>> VerifySettingsAsync(VerifySettingsArgs args)
        {
            await Task.CompletedTask;
            return [];
        }

        public static void SaveSettings(string dataDir, GravitonPluginSettings settings)
        {
            var setFile = Path.Combine(dataDir, "settings.json");
            try
            {
                File.WriteAllText(setFile, JsonSerializer.Serialize<GravitonPluginSettings>(settings));
            }
            catch (Exception ex)
            {
                GravitonNotify.Add(new GravitonNotification("graviton.settings.save.failed", Loc.GetString("SettingSaveFailed", ("Error", ex.Message)), GravitonSeverity.Error, ex));
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
                    settings = JsonSerializer.Deserialize<GravitonPluginSettings>(file);
                    if (settings != null)
                    {
                        foreach (var mapping in settings.Mappings)
                        {
                            mapping.AvailablePlatforms = settings.AccountState.RomMPlatforms;
                        }
                    }
                }
                catch (Exception ex)
                {
                    GravitonNotify.Add(new GravitonNotification("graviton.settings.load.failed", Loc.GetString("SettingLoadFailed", ("Error", ex.Message)), GravitonSeverity.Error, ex));
                }
            }

            if (settings is null)
            {
                return new GravitonPluginSettings();
            }
            else
            {
                return settings;
            }
        }       
    }
}