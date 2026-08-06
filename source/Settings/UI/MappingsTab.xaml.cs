using Emunight;

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

        public void RefreshAvailableEmulators()
        {
            var emulators = ((IEnumerable<EmulatorBase>)_plugin.EmunightAPI!.ImportedEmulators).Concat(_plugin.EmunightAPI.CustomEmulators).OrderBy(e => e.Name).ToObservableCollection();

            foreach (var mapping in _plugin.Settings.Mappings)
            {
                mapping.AvailableEmulators = emulators;
            }
        }

        private static void OnSelectedMappingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var tab = (MappingsTab)d;
            var mapping = e.NewValue as EmulatorMapping;

            tab.MappingOptions.DataContext = mapping;
            tab.MappingOptions.Visibility = mapping != null ? Visibility.Visible : Visibility.Collapsed;

        }

        public static readonly RoutedEvent ManageSavesRequestedEvent = EventManager.RegisterRoutedEvent("ManageSavesRequested", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(MappingsTab));

        public event RoutedEventHandler ManageSavesRequested
        {
            add => AddHandler(ManageSavesRequestedEvent, value);
            remove => RemoveHandler(ManageSavesRequestedEvent, value);
        }

        public MappingsTab()
        {
            InitializeComponent();

            MappingPanel.IsEnabled = _plugin.GameSessionHandlers.Count() <= 0;
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
            var emulators = ((IEnumerable<EmulatorBase>)_plugin.EmunightAPI!.ImportedEmulators).Concat(_plugin.EmunightAPI.CustomEmulators).OrderBy(e => e.Name).ToObservableCollection();

            _plugin.Settings.Mappings.Add(new EmulatorMapping(emulators, _plugin.Settings.AccountState.RomMPlatforms));
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

            var tab = new SaveManagementView();
            _ = tab.Load(SelectedMapping);

            RaiseEvent(new ManageSavesRequestedEventArgs(ManageSavesRequestedEvent, tab));
        }

        private async void Click_BrowseSaveDestination(object sender, RoutedEventArgs e)
        {
            var path = await GravitonPlugin.PlayniteApi.Dialogs.SelectFolderAsync();

            if (SelectedMapping != null && path != null)
                SelectedMapping.SavePath = path[0];

            e.Handled = true;
        }
    }

    public class ManageSavesRequestedEventArgs : RoutedEventArgs
    {
        public SaveManagementView Tab { get; }
        public ManageSavesRequestedEventArgs(RoutedEvent routedEvent, SaveManagementView tab) : base(routedEvent) => Tab = tab;
    }
}
