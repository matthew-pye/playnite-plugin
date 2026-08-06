using Playnite;

namespace Graviton.Saves
{
    internal class SaveController
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private ILogger _logger;

        internal SaveDiscovery Discover { get; private set; }
        internal SaveManager Manager { get; private set; }
        internal SaveNegotiator Negotiator { get; private set; }

        public SaveController(GravitonPlugin plugin, IPlayniteApi playniteAPI, ILogger logger)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;


            Discover = new(plugin, playniteAPI, logger);
            Manager = new(plugin, playniteAPI, logger);
            Negotiator = new(plugin, playniteAPI, logger);
        }
    }
}
