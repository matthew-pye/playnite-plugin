using Graviton.Install;
using Graviton.Install.Downloads;
using Graviton.Models.Install;
using Graviton.Models.Notifications;
using Graviton.Models.ROM;
using Graviton.Models.RomM.Rom;

using Playnite;

using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Graviton.GameEdit
{
    public partial class UpdateDLCManagement : UserControl
    {
        public RomMRomLocal Game { get; }

        public UpdateDLCManagement(RomMRomLocal game)
        {
            Game = game;

            InitializeComponent();
            MainGrid.DataContext = this;
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.DataContext is not UpdateDLCCandidate candidate)
                return;

            if (candidate.Status == Install.InstallStatus.NotInstalled)
            {
                await InstallCandidate(candidate);
            }
            else
            {
                await UninstallCandidate(candidate);
            }

        }

        private async void Reinstall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem || menuItem.DataContext is not UpdateDLCCandidate candidate)
                return;

            await InstallCandidate(candidate);
        }

        private void CandidateMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.ContextMenu == null)
                return;

            button.ContextMenu.DataContext = button.DataContext;
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }

        private async Task InstallCandidate(UpdateDLCCandidate candidate)
        {
            GameInstallInfo installInfo = new()
            {
                Id = Game.Id,
                FileName = Game.FileName ?? "",
                GameName = Game.Name ?? "",
                HasMultipleFiles = Game.HasMultipleFiles,
                DownloadURL = Game.DownloadURL ?? "",
                InstallPath = Game.InstallPath ?? "",
                PatchFileID = Game.PatchFileId,
                Mapping = GravitonPlugin.Instance.Settings.Mappings.FirstOrDefault(x => x.MappingId == Game.MappingID),
                SaveTarget = Game.SaveTarget,
                TitleID = Game.TitleID,

            };
            GravitonPlugin.Logger?.Trace($"Created install info\n{JsonSerializer.Serialize(installInfo, new JsonSerializerOptions { WriteIndented = true })}");

            if (installInfo.Mapping == null)
                throw new Exception(Loc.GetString("InstallMappingNotFound"));

            await GravitonInstallController.InstallSingleCandidate(installInfo, candidate, candidate.Category ?? "");
        }

        private async Task UninstallCandidate(UpdateDLCCandidate candidate)
        {
            if (candidate.PreviousInstallStyle == InstallStyles.CLI)
            {
                var result = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync(Loc.GetString("WarnCLIInstalled"), button: Playnite.MessageBoxButtons.OKCancel, severity: Playnite.MessageBoxSeverity.Warning);

                if (result == Playnite.MessageBoxResult.OK)
                {
                    candidate.InstalledFileIDs.Clear();
                    candidate.InstalledTopPaths.Clear();
                    candidate.InstalledSize = 0;
                    candidate.Status = InstallStatus.NotInstalled;
                }
            }
            else
            {
                var result = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync(Loc.GetString("CandidateUninstallAYS"), button: Playnite.MessageBoxButtons.YesNo, severity: Playnite.MessageBoxSeverity.Warning);
                if (result != Playnite.MessageBoxResult.Yes)
                    return;

                List<UpdateDLCCandidate> candidateList = [..Game.UpdateCandidates, ..Game.DLCCandidates];

                if (candidateList == null)
                    return;

                var collisions = candidateList.Where(x => x != candidate && x.InstalledTopPaths.Any(y => candidate.InstalledTopPaths.Contains(y, StringComparer.OrdinalIgnoreCase))).ToList();

                if (collisions.Count > 0)
                {
                    result = await GravitonPlugin.PlayniteApi.Dialogs.ShowMessageAsync(Loc.GetString("WarnCandidateOverlap"), button: Playnite.MessageBoxButtons.YesNoCancel, severity: Playnite.MessageBoxSeverity.Warning);

                    if (result == Playnite.MessageBoxResult.Cancel)
                        return;

                    if (result == Playnite.MessageBoxResult.Yes)
                    {
                        foreach (var collision in collisions)
                        {
                            await GravitonUninstallController.UninstallCandidate(collision);
                        }
                    }
                }
                    
                await GravitonUninstallController.UninstallCandidate(candidate);
            }

            Game.Save();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RefreshButton.IsEnabled = false;

                UpdateLoadingBar.Visibility = Visibility.Visible;
                DLCLoadingBar.Visibility = Visibility.Visible;

                NoUpdateText.Visibility = Visibility.Collapsed;
                UpdateListBox.Visibility = Visibility.Collapsed;
                NoDLCText.Visibility = Visibility.Collapsed;
                DLCListBox.Visibility = Visibility.Collapsed;

                var response = await GravitonPlugin.RomMServer.GETAsync($"/api/roms/{Game.Id}");

                if (response == null)
                    throw new Exception("No Response from server");

                var rom = JsonSerializer.Deserialize<RomMRom>(response);

                if (rom == null)
                    throw new Exception("Failed to deserialize ROM");

                var mapping = GravitonPlugin.Instance.Settings.Mappings.FirstOrDefault(x => x.MappingId == Game.MappingID);

                if (mapping == null)
                    throw new Exception(Loc.GetString("InstallMappingNotFound"));

                await InstallUpdateDLC.RefreshCandidates(mapping, rom, Game);
            }
            catch (Exception ex)
            {
                GravitonNotify.Notify("graviton.updatedlc.refresh.failed", Loc.GetString("CandidateRefreshFailed", ("Error", ex.Message)), GravitonSeverity.Error, ex);
            }
            finally
            {
                RefreshButton.IsEnabled = true;

                UpdateLoadingBar.Visibility = Visibility.Collapsed;
                DLCLoadingBar.Visibility = Visibility.Collapsed;

                NoDLCText.Visibility = Game.DLCCandidates.Count < 1 ? Visibility.Visible : Visibility.Collapsed;
                DLCListBox.Visibility = Game.DLCCandidates.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

                NoUpdateText.Visibility = Game.UpdateCandidates.Count < 1 ? Visibility.Visible : Visibility.Collapsed;
                UpdateListBox.Visibility = Game.UpdateCandidates.Count > 0 ? Visibility.Visible : Visibility.Collapsed; 
            }
        }
    }
}