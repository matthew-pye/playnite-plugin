using Graviton;
using Graviton.Models.RomM.Rom;

using Playnite;

internal static class GravitonSiblingMerger
{
    internal static async Task MergeSiblings(GravitonPlugin Plugin, List<RomMRom> ROMs)
    {
        foreach (var ROM in ROMs)
        {
            if (ROM.Processed)
                continue;


            if (ROM.Siblings?.Count > 0)
            {
                if (!Plugin.ImportedGames!.ContainsKey($"{ROM.Id}:{ROM.SHA1}") || string.IsNullOrEmpty(Plugin.ImportedGames![$"{ROM.Id}:{ROM.SHA1}"].PlayniteID))
                    continue;

                var game = GravitonPlugin.PlayniteApi.Library.Games.Get(Plugin.ImportedGames![$"{ROM.Id}:{ROM.SHA1}"].PlayniteID!)!;

                List<(RomMRom ROM, Game Game)> SiblingROMs = new List<(RomMRom ROM, Game Game)>();
                foreach (var sibling in ROM.Siblings)
                {
                    if (ROMs.Any(x => x.Id == sibling.Id))
                    {
                        // Check to see if sibling is apart of the ROMs being imported
                        var siblingROM = ROMs.FirstOrDefault(x => x.Id == sibling.Id);
                        if (siblingROM == null)
                            continue;

                        // Check to see if sibling has been imported
                        if (Plugin.ImportedGames!.ContainsKey($"{siblingROM.Id}:{siblingROM.SHA1}") && !string.IsNullOrEmpty(Plugin.ImportedGames![$"{siblingROM.Id}:{siblingROM.SHA1}"].PlayniteID))
                        {
                            var siblingGame = GravitonPlugin.PlayniteApi.Library.Games.Get(Plugin.ImportedGames![$"{siblingROM.Id}:{siblingROM.SHA1}"].PlayniteID!)!;
                            SiblingROMs.Add((siblingROM, siblingGame));
                        }
                    }
                }

                bool foundMergedSiblings = false;

                foreach (var relation in GravitonPlugin.PlayniteApi.Library.GameRelations)
                {
                    // Check to see if game is already the primary game in a game relation
                    if (relation.PrimaryGame == game.Id)
                    {
                        foreach (var sibling in SiblingROMs)
                        {
                            relation.LinkedGames.Add(sibling.Game.Id);
                            sibling.ROM.Processed = true;
                        }

                        await GravitonPlugin.PlayniteApi.Library.GameRelations.UpdateAsync(relation);
                        foundMergedSiblings = true;
                        break;
                    }

                    // Check to see if sibling is already the primary game in a game relation and add this game to the linked games
                    if (SiblingROMs.Any(x => x.Game.Id == relation.PrimaryGame))
                    {
                        relation.LinkedGames.Add(game.Id);
                        await GravitonPlugin.PlayniteApi.Library.GameRelations.UpdateAsync(relation);
                        foundMergedSiblings = true;
                        break;
                    }
                }

                if (foundMergedSiblings)
                    continue;


                //Check to see if ROM is the main sibling
                MainSibling isMainSibling = MainSibling.None;
                if (ROM.RomUser != null && ROM.RomUser.IsMainSibling)
                {
                    isMainSibling = MainSibling.Current;
                }
                else if (ROM.Siblings != null && ROM.Siblings.Count > 0)
                {
                    //Find if there is a main sibling
                    foreach (var sibling in SiblingROMs)
                    {
                        if (sibling.ROM.RomUser != null && sibling.ROM.RomUser.IsMainSibling)
                        {
                            isMainSibling = MainSibling.Other;
                            break;
                        }
                    }
                }

                // Create new game relation if either there is no main sibling or the current game is the main sibling
                if (isMainSibling != MainSibling.Other)
                {
                    GameRelation newgamerelation = new GameRelation();
                    newgamerelation.PrimaryGame = game.Id;
                    foreach (var sibling in SiblingROMs)
                    {
                        newgamerelation.LinkedGames.Add(sibling.Game.Id);
                        sibling.ROM.Processed = true;
                    }

                    await GravitonPlugin.PlayniteApi.Library.GameRelations.AddAsync(newgamerelation);
                }
            }
        }
    }
}