using Graviton.Models.RomM.Saves;

using System.Windows;
using System.Windows.Controls;

namespace Graviton.Saves
{
    public partial class ResolveConflictView : UserControl
    {
        public SaveSyncStatus Status = SaveSyncStatus.no_op;

        public string ServerUpdatedAt;
        public string LocalUpdatedAt;

        public ResolveConflictView(DateTime serverLastUpdated, DateTime localLastUpdated)
        {
            ServerUpdatedAt = serverLastUpdated.ToString("O");
            LocalUpdatedAt = localLastUpdated.ToString("O");

            InitializeComponent();

            MainGrid.DataContext = this;
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            ((Window)Parent).Close();
        }

        private void KeepLocal_Click(object sender, RoutedEventArgs e)
        {
            Status = SaveSyncStatus.upload;
            ((Window)Parent).Close();
        }

        private void KeepRemote_Click(object sender, RoutedEventArgs e)
        {
            Status = SaveSyncStatus.download;
            ((Window)Parent).Close();
        }
    }
}