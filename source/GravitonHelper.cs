namespace Graviton
{
    internal static class GravitonHelper
    {
        internal static bool TryParseGameID(string? gameID, out int ID)
        {
			try
			{
                if(string.IsNullOrEmpty(gameID))
                    throw new Exception("Game ID is null or empty");

                if (int.TryParse(gameID, out ID))
                    return true;
               
                throw new Exception($"Failed to parse {gameID} into int");
            }
			catch (Exception ex)
			{
                GravitonPlugin.Logger.Error($"[GravitonHelper] {ex.Message}");

                ID = -1;
                return false;
			}
        }

    }

}