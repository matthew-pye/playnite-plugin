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

        internal SaveDiscovery Discover { get; private set; }
        internal SaveManager Manager { get; private set; }
        internal SaveNegotiator Negotiator { get; private set; }

        internal SaveController(IGravitonContext plugin, IPlayniteApi playniteAPI, GravitonLogger logger, IRomMServer romMServer)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
            _romMServer = romMServer;

            Discover = new(plugin, playniteAPI, logger, romMServer);
            Manager = new(plugin, playniteAPI, logger, romMServer);
            Negotiator = new(plugin, playniteAPI, logger, romMServer);            
        }
    }
}
