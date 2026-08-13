namespace Graviton
{
    internal static class GravitonHelper
    {
        internal static bool TryParseGameID(string? gameID, out int ID, out string SHA1)
        {
			try
			{
                if(string.IsNullOrEmpty(gameID))
                    throw new Exception("Game ID is null or empty");

                var splitID = gameID.Split(':');

                if (splitID.Length != 2)
                    throw new Exception("Game ID doesn't contain 2 elements");

                ID = int.Parse(splitID[0]);
                SHA1 = splitID[1];
                return true;
			}
			catch (Exception ex)
			{
                GravitonPlugin.Logger.Error($"[GravitonHelper] {ex.Message}");

                ID = -1;
                SHA1 = "";
                return false;
			}
        }

    }

}