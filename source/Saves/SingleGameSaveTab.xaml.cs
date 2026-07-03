using Graviton.Models;
using Graviton.Models.RomM;
using Graviton.Models.RomM.Saves;

using Playnite;

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Graviton.Saves
{
    public partial class SinglegameSaveTab : UserControl
    {
        private GravitonPlugin _plugin => GravitonPlugin.Instance;

        public Game? Game { get; private set; }
        public EmulatorMapping? Mapping { get; private set; }
        public ObservableCollection<RomMNegotiateSave> RemoteSaves { get; } = new();

        public SinglegameSaveTab()
        {
            InitializeComponent();
            RemoteList.ItemsSource = RemoteSaves;
        }

        public void LoadForGame(Game game, EmulatorMapping mapping)
        {
            Game = game;
            Mapping = mapping;

            GameNameText.Text = game.Name;
            ManualPickHintText.Visibility = SetAsSaveButton.Visibility = mapping.SaveLayout == SaveLayoutStyle.ManualPerGame ? Visibility.Visible : Visibility.Collapsed;

        }

        private void SetAsSave_Click(object sender, RoutedEventArgs e) 
        { 
            /* TODO: claim selected local file(s) or folders for this game */ 
        }

        private void UploadNew_Click(object sender, RoutedEventArgs e) 
        { 
            
        }

        private void KeepLocal_Click(object sender, RoutedEventArgs e) 
        { 
           
        }

        private void KeepRemote_Click(object sender, RoutedEventArgs e) 
        { 
           
        }

        private void SyncEnabled_Changed(object sender, RoutedEventArgs e) 
        { 
            
        }
    }
}