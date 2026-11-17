using Graviton.Notifications;

using Playnite;

namespace Graviton.Saves
{
    public class SaveController
    {
        private IGravitonContext _plugin;
        private IPlayniteApi _playniteAPI;
        private GravitonLogger _logger;
        private IRomMServer _romMServer;


        internal SaveController(IGravitonContext plugin, IPlayniteApi playniteAPI, GravitonLogger logger, IRomMServer romMServer)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
            _romMServer = romMServer;
        }
    }
}
