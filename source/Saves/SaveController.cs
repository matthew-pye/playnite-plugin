using Playnite;

namespace Graviton.Saves
{
    internal class SaveController
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private ILogger _logger;
        private IRomMServer _romMServer;

        internal SaveDiscovery Discover { get; private set; }
        internal SaveManager Manager { get; private set; }
        internal SaveNegotiator Negotiator { get; private set; }

        public SaveController(GravitonPlugin plugin, IPlayniteApi playniteAPI, ILogger logger, IRomMServer romMServer)
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
