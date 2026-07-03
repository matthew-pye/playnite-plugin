using Playnite;

namespace Graviton.Saves
{
    internal class SaveStateController
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private ILogger _logger;

        public SaveStateController(GravitonPlugin plugin, IPlayniteApi playniteAPI, ILogger logger)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
        }

    }
}
