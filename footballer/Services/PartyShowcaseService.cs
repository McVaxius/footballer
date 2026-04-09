using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using footballer.Models;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace footballer.Services;

public sealed class PartyShowcaseService
{
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly IPartyList partyList;

    public PartyShowcaseService(IClientState clientState, IObjectTable objectTable, IPartyList partyList)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.partyList = partyList;
    }

    public IReadOnlyList<PartyShowcaseMember> CaptureCurrentPartySnapshot()
    {
        var members = new List<PartyShowcaseMember>();
        if (!clientState.IsLoggedIn)
            return members;

        var playerCharacters = objectTable
            .OfType<IPlayerCharacter>()
            .Where(playerCharacter => !string.IsNullOrWhiteSpace(playerCharacter.Name.TextValue))
            .ToList();

        if (partyList.Length > 0)
        {
            for (var i = 0; i < partyList.Length; i++)
            {
                var member = partyList[i];
                if (member == null)
                    continue;

                var name = member.Name.TextValue;
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var worldName = member.World.IsValid ? member.World.Value.Name.ToString() : string.Empty;
                var job = member.ClassJob.IsValid ? member.ClassJob.Value.Abbreviation.ToString() : "UNK";
                var contentId = member.ContentId > 0 ? ((ulong)member.ContentId).ToString("X16") : string.Empty;
                var matchedCharacter = FindPlayerCharacter(playerCharacters, name, worldName);
                var isLocalPlayer = matchedCharacter != null && objectTable.LocalPlayer != null && matchedCharacter.GameObjectId == objectTable.LocalPlayer.GameObjectId;
                var sexLabel = matchedCharacter?.CustomizeData.Sex.ToString() ?? member.Sex.ToString();
                var raceLabel = matchedCharacter?.CustomizeData.Race.ToString() ?? "Unknown";
                var tribeLabel = matchedCharacter?.CustomizeData.Tribe.ToString() ?? "Unknown";
                var hasCustomizeData = matchedCharacter != null;

                members.Add(new PartyShowcaseMember(
                    i + 1,
                    name,
                    worldName,
                    job,
                    member.ClassJob.RowId,
                    member.Level,
                    contentId,
                    matchedCharacter?.EntityId ?? 0,
                    isLocalPlayer,
                    sexLabel,
                    raceLabel,
                    tribeLabel,
                    hasCustomizeData));
            }

            return members;
        }

        var localPlayer = objectTable.LocalPlayer;
        if (localPlayer == null)
            return members;

        members.Add(new PartyShowcaseMember(
            1,
            localPlayer.Name.TextValue,
            localPlayer.HomeWorld.Value.Name.ToString(),
            localPlayer.ClassJob.Value.Abbreviation.ToString(),
            localPlayer.ClassJob.RowId,
            localPlayer.Level,
            string.Empty,
            localPlayer.EntityId,
            true,
            localPlayer.CustomizeData.Sex.ToString(),
            localPlayer.CustomizeData.Race.ToString(),
            localPlayer.CustomizeData.Tribe.ToString(),
            true));

        return members;
    }

    private static IPlayerCharacter? FindPlayerCharacter(
        IReadOnlyList<IPlayerCharacter> playerCharacters,
        string name,
        string worldName)
        => playerCharacters.FirstOrDefault(playerCharacter =>
            string.Equals(playerCharacter.Name.TextValue, name, StringComparison.OrdinalIgnoreCase) &&
            (MatchesWorld(playerCharacter.HomeWorld, worldName) || MatchesWorld(playerCharacter.CurrentWorld, worldName)));

    private static bool MatchesWorld(RowRef<World> world, string worldName)
        => !string.IsNullOrWhiteSpace(worldName) &&
           world.IsValid &&
           string.Equals(world.Value.Name.ToString(), worldName, StringComparison.OrdinalIgnoreCase);
}
