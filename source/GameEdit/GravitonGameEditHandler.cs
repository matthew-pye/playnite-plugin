using Graviton.Models.ROM;
using Graviton.Notifications;

using Playnite;

namespace Graviton.GameEdit
{
    public class GravitonGameEditHandler : GameEditSessionHandler
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private GravitonLogger _logger;

        private RomMRomLocal? _game;


        public GravitonGameEditHandler(GravitonPlugin plugin, IPlayniteApi playniteAPI, GravitonLogger logger, Game game)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;

            plugin.ImportedGames.TryGetValue(game.LibraryGameId ?? "-1", out _game);

        }

        public override async Task<List<GameEditSessionSection>> GetEditSectionsAsync(GetEditSectionsAsyncArgs args)
        {
            List<GameEditSessionSection> gameEditSections = new();

            if(_game != null)
            {
                gameEditSections.Add(new(Loc.GetString("UpdateDLCSection"), new UpdateDLCManagement(_game)));
                //gameEditSections.Add(new(Loc.GetString("ManageSave"), ));
            }

            return gameEditSections;
        }

        public override bool GetHasUnsavedChanges(GetHasUnsavedChangesArgs args)
        {
            return false;
        }

        public override async Task EndEditAsync(EndEditArgs args) 
        {
            
        }
    }
}
