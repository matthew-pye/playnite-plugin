using Graviton.Models;
using Graviton.Models.Notifications;
using Graviton.Saves;

using Playnite;

using System.Windows;
using System.Windows.Controls;

namespace Graviton.Settings
{
    /// <summary>
    /// Interaction logic for GravitonSettingsView.xaml
    /// </summary>
    public partial class MappingsTab : UserControl
    {
        private GravitonPlugin _plugin { get => GravitonPlugin.Instance; }

        public static readonly DependencyProperty SelectedMappingProperty = DependencyProperty.Register(nameof(SelectedMapping), typeof(EmulatorMapping), typeof(MappingsTab), new PropertyMetadata(null, OnSelectedMappingChanged));

        public EmulatorMapping? SelectedMapping
        {
            get => (EmulatorMapping)GetValue(SelectedMappingProperty);
            set => SetValue(SelectedMappingProperty, value);
        }

        private static void OnSelectedMappingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var tab = (MappingsTab)d;
            var mapping = e.NewValue as EmulatorMapping;

            tab.MappingOptions.DataContext = mapping;
            tab.MappingOptions.Visibility = mapping != null ? Visibility.Visible : Visibility.Collapsed;

        }

        public MappingsTab()
        {
            InitializeComponent();

            SyncPlatformsText.Text = $"\uf46a {Loc.GetString("SyncPlatforms")}";
            AddMappingText.Text = $"\uea60 {Loc.GetString("NewMapping")}";

            EnabledText.Text = Loc.GetString("MappingEnabled");
            EmulatorText.Text = Loc.GetString("Emulator");
            ProfileText.Text = Loc.GetString("Profile");
            PlatformText.Text = Loc.GetString("Platform");
            ROMLocText.Text = Loc.GetString("ROMLoc");
            ROMLocationPlaceholder.Text = Loc.GetString("NoFolderPlaceholder");
            BrowseROMLocationText.Text = Loc.GetString("Browse");
           
            OptionText.Text = Loc.GetString("Options");
            ExtractArchiveROMsText.Text = Loc.GetString("AutoExtractROMs");
            ExtractArchiveROMs.ToolTip = Loc.GetString("AutoExtractROMsTooltip");
            Preferm3uText.Text = Loc.GetString("PreferM3U");
            Preferm3u.ToolTip = Loc.GetString("PreferM3UTooltip");

            SaveOptionText.Text = Loc.GetString("SaveOptions");
            AutoDetectionStyleText.Text = Loc.GetString("AutoSaveDetection");
            AutoDetectionStyleCombo.ToolTip = Loc.GetString("AutoSaveDetectionTooltip");
            SaveExtensionsText.Text = Loc.GetString("SaveExtensions");
            SaveLocText.Text = Loc.GetString("SaveLocation");
            SavePathPlaceHolder.Text = Loc.GetString("NoFolderPlaceholder");
            SaveLocButtonText.Text = Loc.GetString("Browse");
            OpenSaveManagerText.Text = $"\uf019 {Loc.GetString("ManageSaves")}";
            
        }

        private async void SyncPlatforms_Click(object sender, RoutedEventArgs e)
        {
            SyncPlatformsButton.IsEnabled = false;

            if(await _plugin.Account!.SyncPlatforms())
                GravitonNotify.Add(new GravitonNotification("graviton.GET.platforms", Loc.GetString("PlatformsSynced", ("PlaformCount", _plugin.Settings.AccountState.RomMPlatforms.Count)), GravitonSeverity.Success));

            SyncPlatformsButton.IsEnabled = true;
            e.Handled = true;
        }
        
        private void AddMapping_Click(object sender, RoutedEventArgs e)
        {
            _plugin.Settings.Mappings.Add(new EmulatorMapping(_plugin.Settings.AccountState.RomMPlatforms));
        }
        
        private async void DeleteMapping_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedMapping != null)
            {
                var response = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync($"{SelectedMapping.GetDescriptionLines()}", Loc.GetString("DeleteMappingConfirmTitle"), Playnite.MessageBoxButtons.YesNoCancel);
         
                if (response == Playnite.MessageBoxResult.Yes)
                {
                    _plugin.Settings.Mappings.Remove(SelectedMapping);
                    MappingOptions.Visibility = Visibility.Collapsed;
                    MappingOptions.DataContext = null;
                }
            }
            
            e.Handled = true;
        }

        private async void BrowseROMLocation_Click(object sender, RoutedEventArgs e)
        {
            var path = await GravitonPlugin.PlayniteApi.Dialogs.SelectFolderAsync();
        
            if (path != null)
                SelectedMapping?.DestinationPath = path[0];
        
            e.Handled = true;

        }

        private void OpenSaveManager_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedMapping == null) 
                return;

            var tab = new MappingSaveTab();
            _ = tab.Load(SelectedMapping);

            SaveManagerWindow.Show(Loc.GetString("SaveManagerTitle"), tab);
        }

        private async void Click_BrowseSaveDestination(object sender, RoutedEventArgs e)
        {
            var path = await GravitonPlugin.PlayniteApi.Dialogs.SelectFolderAsync();

            if (SelectedMapping != null && path != null)
                SelectedMapping.SavePath = path[0];

            e.Handled = true;
        }
    }
}
