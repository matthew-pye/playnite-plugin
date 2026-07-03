using Playnite;

namespace Graviton.Saves
{
    internal class SaveController
    {
        private GravitonPlugin _plugin;
        private IPlayniteApi _playniteAPI;
        private ILogger _logger;

        public SaveController(GravitonPlugin plugin, IPlayniteApi playniteAPI, ILogger logger)
        {
            _plugin = plugin;
            _playniteAPI = playniteAPI;
            _logger = logger;
        }


        public async Task NegotiateSaves()
        {

        }

    }
}
