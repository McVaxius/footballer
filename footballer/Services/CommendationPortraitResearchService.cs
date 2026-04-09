using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using footballer.Models;

namespace footballer.Services;

public sealed unsafe class CommendationPortraitResearchService
{
    private readonly IGameGui gameGui;
    private readonly Configuration configuration;
    private string? lastCaptureErrorSignature;

    private static readonly (string AddonName, int? AgentId, string Role)[] ProbeTemplates =
    {
        ("BannerPreview", null, "Preview window used during portrait updates"),
        ("BannerParty", 423, "Current display-party-member-portraits target"),
        ("BannerMIP", 424, "Not current target; older commendation candidate"),
        ("BannerList", 392, "Portrait list / selection surface"),
        ("BannerEditor", 393, "Portrait editor surface"),
    };

    public CommendationPortraitResearchService(IGameGui gameGui, Configuration configuration)
    {
        this.gameGui = gameGui;
        this.configuration = configuration;
    }

    public CommendationPortraitResearchSnapshot CaptureSnapshot()
    {
        var probes = new CommendationAddonProbe[ProbeTemplates.Length];
        var anyVisible = false;
        var captureErrors = new List<string>();

        for (var i = 0; i < ProbeTemplates.Length; i++)
        {
            var template = ProbeTemplates[i];
            var visible = IsAddonVisible(template.AddonName);
            anyVisible |= visible;
            probes[i] = new CommendationAddonProbe(template.AddonName, template.AgentId, template.Role, visible);
        }

        PortraitAddonNodeSnapshot? bannerPartySnapshot = null;
        BannerPartyAgentSnapshot? bannerPartyAgentSnapshot = null;

        try
        {
            bannerPartySnapshot = CaptureBannerPartySnapshot();
        }
        catch (Exception ex)
        {
            captureErrors.Add($"node capture: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            bannerPartyAgentSnapshot = CaptureBannerPartyAgentSnapshot(bannerPartySnapshot);
        }
        catch (Exception ex)
        {
            captureErrors.Add($"agent capture: {ex.GetType().Name}: {ex.Message}");
        }

        string? captureError = captureErrors.Count > 0
            ? string.Join(" | ", captureErrors)
            : null;
        if (!string.Equals(lastCaptureErrorSignature, captureError, StringComparison.Ordinal))
        {
            lastCaptureErrorSignature = captureError;
            if (captureError is not null)
                Plugin.Log.Warning("[footballer] Failed to capture BannerParty research snapshot safely: {CaptureError}", captureError);
        }

        var safeStatus = captureError is not null
            ? $"Relevant portrait UI is open right now, but BannerParty capture failed safely instead of crashing the window: {captureError}"
            : bannerPartyAgentSnapshot?.ActiveExportPayload is not null
                ? "Relevant portrait UI is open right now. This build now captures live BannerParty addon truth, live agent rows, and a read-only export payload for the active row; no portrait write path is active."
                : anyVisible
                    ? "Relevant portrait UI is open right now. This build captures live BannerParty addon truth plus agent-row state, but still keeps the portrait path read-only."
                : "No relevant portrait UI is open right now. Open the display party member portraits window or another portrait surface to probe live addons.";
        var nextStep = captureError is not null
            ? "Re-open the display party member portraits window and confirm the main window stays stable. If another capture error appears, send the new text."
            : bannerPartyAgentSnapshot?.ActiveExportPayload is not null
                ? "Change the selected party portrait row and confirm the active-row mapping and read-only payload change with it. Keep write-path work deferred until that read path stays stable."
                : bannerPartyAgentSnapshot is not null
                    ? "Use the BannerParty agent rows and rail mapping below to confirm which party row is currently selected, then keep the portrait path read-only until the export stays stable."
                    : anyVisible
                        ? "Capture addon/node truth for the visible surface before attempting any replacement write path, with BannerParty as the current target."
                        : "Open a portrait flow and re-check BannerPreview, BannerParty, and BannerMIP.";

        return new CommendationPortraitResearchSnapshot(
            configuration.ReplaceCommendationPictures,
            anyVisible,
            safeStatus,
            "Known local seam: BannerPreview callback 0 is used elsewhere in the workspace to finalize portrait updates.",
            nextStep,
            probes,
            bannerPartySnapshot,
            bannerPartyAgentSnapshot);
    }

    private PortraitAddonNodeSnapshot? CaptureBannerPartySnapshot()
    {
        var addonAddress = GetAddonAddress("BannerParty");
        if (addonAddress == nint.Zero)
            return null;

        var addon = (AtkUnitBase*)addonAddress;
        var nodes = new List<PortraitAddonNodeEntry>(addon->UldManager.NodeListCount);

        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
        {
            var node = addon->UldManager.NodeList[i];
            if (node == null)
                continue;

            nodes.Add(CaptureNode(i, node));
        }

        var slots = BuildLikelyPortraitSlots(nodes);
        var visibleNodes = nodes.Count(node => node.Visible);
        var interactiveNodes = nodes.Count(node => node.AppearsInteractive);
        var repeatedHeaders = nodes.Count(node => string.Equals(node.Text, "Party Members", StringComparison.Ordinal));
        var captureStatus =
            $"Captured {nodes.Count} live BannerParty nodes. Visible nodes: {visibleNodes}. Interactive or event-bearing nodes: {interactiveNodes}. Repeated \"Party Members\" headers: {repeatedHeaders}. Likely 240x540 portrait rails: {slots.Length}.";

        return new PortraitAddonNodeSnapshot(
            "BannerParty",
            addonAddress,
            addon->X,
            addon->Y,
            addon->UldManager.NodeListCount,
            captureStatus,
            nodes.ToArray(),
            slots);
    }

    private static BannerPartyAgentSnapshot? CaptureBannerPartyAgentSnapshot(PortraitAddonNodeSnapshot? bannerPartySnapshot)
    {
        var agent = AgentBannerParty.Instance();
        if (agent == null || agent->Data == null)
            return null;

        var storage = agent->Data;
        var charactersSpan = storage->Characters;
        var characters = new List<BannerPartyCharacterSnapshot>(charactersSpan.Length);

        for (var i = 0; i < charactersSpan.Length; i++)
        {
            fixed (AgentBannerInterface.Storage.CharacterData* character = &charactersSpan[i])
            {
                var snapshot = CaptureCharacterSnapshot(i, character);
                if (IsMeaningfulCharacterSnapshot(snapshot))
                    characters.Add(snapshot);
            }
        }

        var activeRowNumber = DetermineActiveCharacterRowNumber(characters);
        var activeCharacterName = activeRowNumber is int rowNumber
            ? characters.FirstOrDefault(character => character.RowIndex == rowNumber)?.Name
            : null;
        PortraitExportPayloadSnapshot? activeExportPayload = null;
        var activeExportStatus = "No active BannerParty row was detected yet.";

        if (activeRowNumber is int activeRow && activeRow > 0 && activeRow <= charactersSpan.Length)
        {
            fixed (AgentBannerInterface.Storage.CharacterData* activeCharacter = &charactersSpan[activeRow - 1])
            {
                activeExportPayload = TryCaptureActiveExportPayload(activeCharacter, out activeExportStatus);
            }
        }

        var railMappings = BuildRailMappings(bannerPartySnapshot, characters, activeRowNumber);
        var visibleRows = characters.Count(character => character.CharacterVisible);
        var loadedRows = characters.Count(character => character.CharacterLoaded);
        var captureStatus =
            $"Captured {characters.Count} live BannerParty agent rows. Visible CharaViews: {visibleRows}. Loaded CharaViews: {loadedRows}. " +
            $"Active row heuristic: {(activeRowNumber ?? 0)}. Rail mappings: {railMappings.Length}.";

        return new BannerPartyAgentSnapshot(
            (nint)agent,
            characters.Count,
            captureStatus,
            activeRowNumber,
            activeCharacterName,
            characters.ToArray(),
            railMappings,
            activeExportPayload,
            activeExportStatus);
    }

    private static PortraitAddonNodeEntry CaptureNode(int index, AtkResNode* node)
    {
        var rawType = (ushort)node->Type;
        var typeName = GetNodeTypeName(rawType);
        var flagsRaw = (ushort)node->NodeFlags;
        var flags = (NodeFlags)flagsRaw;
        var (eventCount, firstEventType) = ReadEventSummary(node);
        var text = TryReadNodeText(node, rawType);
        var appearsInteractive =
            rawType >= 1000 ||
            eventCount > 0 ||
            flags.HasFlag(NodeFlags.Enabled) ||
            flags.HasFlag(NodeFlags.RespondToMouse) ||
            flags.HasFlag(NodeFlags.EmitsEvents) ||
            flags.HasFlag(NodeFlags.HasCollision);

        return new PortraitAddonNodeEntry(
            index,
            node->NodeId,
            typeName,
            rawType,
            flags.HasFlag(NodeFlags.Visible),
            node->X,
            node->Y,
            node->Width,
            node->Height,
            node->Priority,
            flagsRaw,
            flags.ToString(),
            eventCount,
            firstEventType,
            appearsInteractive,
            text,
            (nint)node);
    }

    private static PortraitSlotCandidate[] BuildLikelyPortraitSlots(IReadOnlyList<PortraitAddonNodeEntry> nodes)
    {
        var baseNodes = nodes
            .Where(node => node.TypeName == "Res" && node.Width == 240 && node.Height == 540)
            .OrderBy(node => node.NodeId)
            .ToList();
        var sliderNodes = nodes
            .Where(node => node.TypeName == "Slider" && node.Width == 240 && node.Height == 540)
            .ToDictionary(node => node.NodeId);
        var results = new List<PortraitSlotCandidate>(baseNodes.Count + sliderNodes.Count);
        var slotIndex = 1;

        foreach (var baseNode in baseNodes)
        {
            sliderNodes.TryGetValue(baseNode.NodeId + 1, out var sliderNode);

            results.Add(new PortraitSlotCandidate(
                slotIndex++,
                baseNode.NodeId,
                sliderNode?.NodeId,
                baseNode.X,
                baseNode.Y,
                baseNode.Width,
                baseNode.Height,
                baseNode.Visible || sliderNode?.Visible == true,
                baseNode.AppearsInteractive || sliderNode?.AppearsInteractive == true,
                sliderNode is null
                    ? "Base portrait rail without an adjacent slider pair."
                    : baseNode.Visible
                        ? "Currently active portrait rail scaffold."
                        : "Inactive portrait rail scaffold."));

            if (sliderNode is not null)
                sliderNodes.Remove(sliderNode.NodeId);
        }

        foreach (var sliderNode in sliderNodes.Values.OrderBy(node => node.NodeId))
        {
            results.Add(new PortraitSlotCandidate(
                slotIndex++,
                0,
                sliderNode.NodeId,
                sliderNode.X,
                sliderNode.Y,
                sliderNode.Width,
                sliderNode.Height,
                sliderNode.Visible,
                sliderNode.AppearsInteractive,
                "Slider-only portrait rail candidate."));
        }

        return results.ToArray();
    }

    private static BannerPartyCharacterSnapshot CaptureCharacterSnapshot(int index, AgentBannerInterface.Storage.CharacterData* character)
    {
        var firstName = NormalizeNodeText(character->Name1.ToString());
        var lastName = NormalizeNodeText(character->Name2.ToString());
        var fullName = string.Join(" ", new[] { firstName, lastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
        if (string.IsNullOrWhiteSpace(fullName))
            fullName = $"Row {index + 1}";

        var job = NormalizeNodeText(character->Job.ToString()) ?? "-";
        var selectionHint = character->CharaView.CharacterVisible
            ? "Current visible portrait render target."
            : character->CharaView.CharacterLoaded
                ? "Portrait is loaded for this row."
                : character->CharaView.CharacterDataCopied
                    ? "Character data is copied but the portrait is not visible yet."
                    : character->CharaView.State != 0
                        ? $"Non-zero CharaView state {character->CharaView.State}."
                        : "Inactive row.";

        return new BannerPartyCharacterSnapshot(
            index + 1,
            fullName,
            job,
            character->WorldId,
            character->CharaView.CharacterVisible,
            character->CharaView.CharacterLoaded,
            character->CharaView.CharacterDataCopied,
            character->CharaView.State,
            character->CharaView.PoseClassJob,
            character->CharaView.PortraitCharacterData.ClassJobId,
            selectionHint);
    }

    private static bool IsMeaningfulCharacterSnapshot(BannerPartyCharacterSnapshot character)
    {
        if (character.CharacterLoaded || character.CharacterDataCopied || character.CharaViewState != 0 || character.WorldId != 0)
            return true;

        if (!string.Equals(character.Job, "-", StringComparison.Ordinal))
            return true;

        return !string.Equals(character.Name, $"Row {character.RowIndex}", StringComparison.Ordinal);
    }

    private static int? DetermineActiveCharacterRowNumber(IReadOnlyList<BannerPartyCharacterSnapshot> characters)
    {
        foreach (var character in characters)
        {
            if (character.CharacterVisible)
                return character.RowIndex;
        }

        foreach (var character in characters)
        {
            if (character.CharacterLoaded)
                return character.RowIndex;
        }

        foreach (var character in characters)
        {
            if (character.CharaViewState != 0)
                return character.RowIndex;
        }

        return null;
    }

    private static PortraitRailRowMapping[] BuildRailMappings(
        PortraitAddonNodeSnapshot? bannerPartySnapshot,
        IReadOnlyList<BannerPartyCharacterSnapshot> characters,
        int? activeRowNumber)
    {
        if (bannerPartySnapshot is null || bannerPartySnapshot.LikelyPortraitSlots.Length == 0)
            return [];

        var orderedSlots = OrderLikelyPortraitSlots(bannerPartySnapshot.LikelyPortraitSlots, out var axisLabel);
        var mappedRailIndices = GetMappedRailIndices(orderedSlots, characters.Count, out var mappingStrategyLabel);
        var activeRailOrder = DetermineActiveRailIndex(orderedSlots, mappedRailIndices, activeRowNumber);
        var results = new PortraitRailRowMapping[orderedSlots.Length];
        var characterByRailIndex = new Dictionary<int, BannerPartyCharacterSnapshot>();

        if (mappedRailIndices.Length > 0 && activeRailOrder >= 0 && activeRowNumber is int rowNumber && rowNumber > 0)
        {
            var activeMappedRailPosition = Array.IndexOf(mappedRailIndices, activeRailOrder);
            if (activeMappedRailPosition < 0)
                activeMappedRailPosition = 0;

            var shift = activeMappedRailPosition - (rowNumber - 1);
            foreach (var character in characters)
            {
                var mappedRailPosition = (character.RowIndex - 1) + shift;
                if (mappedRailPosition >= 0 && mappedRailPosition < mappedRailIndices.Length)
                    characterByRailIndex[mappedRailIndices[mappedRailPosition]] = character;
            }
        }
        else
        {
            for (var i = 0; i < characters.Count && i < mappedRailIndices.Length; i++)
                characterByRailIndex[mappedRailIndices[i]] = characters[i];
        }

        for (var i = 0; i < orderedSlots.Length; i++)
        {
            var slot = orderedSlots[i];
            characterByRailIndex.TryGetValue(i, out var character);
            var likelySelected = activeRailOrder == i || (activeRowNumber is int activeSelectedRow && character?.RowIndex == activeSelectedRow && slot.AnyVisible);
            var mappingNote = character is null
                ? $"Ordered by {axisLabel}; {mappingStrategyLabel}; no live BannerParty agent row is mapped to this rail position."
                : activeRailOrder >= 0 && activeRowNumber is int
                    ? $"Ordered by {axisLabel}; {mappingStrategyLabel}; mapped to live BannerParty row {character.RowIndex} by aligning the active row to the preferred rail set."
                    : $"Ordered by {axisLabel}; {mappingStrategyLabel}; mapped to live BannerParty row {character.RowIndex} by agent storage order.";
            if (likelySelected)
                mappingNote += " This is the current active rail candidate.";

            results[i] = new PortraitRailRowMapping(
                i + 1,
                slot.BaseNodeId,
                slot.SliderNodeId,
                slot.X,
                slot.Y,
                slot.Width,
                slot.Height,
                slot.AnyVisible,
                slot.AnyInteractive,
                character?.RowIndex,
                character?.Name,
                character?.Job,
                likelySelected,
                mappingNote);
        }

        return results;
    }

    private static int DetermineActiveRailIndex(
        PortraitSlotCandidate[] orderedSlots,
        IReadOnlyList<int> mappedRailIndices,
        int? activeRowNumber)
    {
        if (activeRowNumber is int rowNumber && rowNumber > 0)
        {
            var mappedRailPosition = rowNumber - 1;
            if (mappedRailPosition >= 0 && mappedRailPosition < mappedRailIndices.Count)
            {
                var preferredRailIndex = mappedRailIndices[mappedRailPosition];
                if (orderedSlots[preferredRailIndex].AnyVisible)
                    return preferredRailIndex;
            }
        }

        return Array.FindIndex(orderedSlots, slot => slot.AnyVisible);
    }

    private static int[] GetMappedRailIndices(
        PortraitSlotCandidate[] orderedSlots,
        int characterCount,
        out string mappingStrategyLabel)
    {
        var visibleRailIndices = Enumerable.Range(0, orderedSlots.Length)
            .Where(index => orderedSlots[index].AnyVisible)
            .ToArray();
        if (characterCount > 0 && visibleRailIndices.Length >= characterCount)
        {
            mappingStrategyLabel = "preferring live visible rails before hidden placeholder scaffolds";
            return visibleRailIndices;
        }

        mappingStrategyLabel = "using the full ordered rail list because visible rails were insufficient";
        return Enumerable.Range(0, orderedSlots.Length).ToArray();
    }

    private static PortraitSlotCandidate[] OrderLikelyPortraitSlots(PortraitSlotCandidate[] slots, out string axisLabel)
    {
        if (slots.Length <= 1)
        {
            axisLabel = "node order";
            return slots.OrderBy(slot => slot.SlotIndex).ToArray();
        }

        var xSpread = slots.Max(slot => slot.X) - slots.Min(slot => slot.X);
        var ySpread = slots.Max(slot => slot.Y) - slots.Min(slot => slot.Y);
        if (ySpread > xSpread)
        {
            axisLabel = "top-to-bottom Y order";
            return slots
                .OrderBy(slot => slot.Y)
                .ThenBy(slot => slot.X)
                .ThenBy(slot => slot.SlotIndex)
                .ToArray();
        }

        axisLabel = "left-to-right X order";
        return slots
            .OrderBy(slot => slot.X)
            .ThenBy(slot => slot.Y)
            .ThenBy(slot => slot.SlotIndex)
            .ToArray();
    }

    private static PortraitExportPayloadSnapshot? TryCaptureActiveExportPayload(
        AgentBannerInterface.Storage.CharacterData* character,
        out string status)
    {
        if (!character->CharaView.CharacterLoaded &&
            !character->CharaView.CharacterVisible &&
            !character->CharaView.CharacterDataCopied &&
            character->CharaView.State == 0)
        {
            status = "Active BannerParty row was found, but its CharaViewPortrait does not look initialized yet so export was skipped.";
            return null;
        }

        ExportedPortraitData payload = default;
        character->CharaView.ExportPortraitData(&payload);
        status =
            $"Read-only ExportedPortraitData captured for active row {NormalizeNodeText(character->Name1.ToString())} {NormalizeNodeText(character->Name2.ToString())}".Trim();

        return new PortraitExportPayloadSnapshot(
            FormatHalfVector4(payload.CameraPosition),
            FormatHalfVector4(payload.CameraTarget),
            payload.ImageRotation,
            payload.CameraZoom,
            payload.BannerTimeline,
            payload.AnimationProgress,
            payload.Expression,
            FormatHalfVector2(payload.HeadDirection),
            FormatHalfVector2(payload.EyeDirection),
            $"{payload.DirectionalLightingColorRed}/{payload.DirectionalLightingColorGreen}/{payload.DirectionalLightingColorBlue}",
            payload.DirectionalLightingBrightness,
            payload.DirectionalLightingVerticalAngle,
            payload.DirectionalLightingHorizontalAngle,
            $"{payload.AmbientLightingColorRed}/{payload.AmbientLightingColorGreen}/{payload.AmbientLightingColorBlue}",
            payload.AmbientLightingBrightness,
            payload.BannerBg);
    }

    private static (int Count, string? FirstEventType) ReadEventSummary(AtkResNode* node)
    {
        var eventCount = 0;
        var current = node->AtkEventManager.Event;

        while (current != null && eventCount < 32)
        {
            eventCount++;
            current = current->NextEvent;
        }

        return (eventCount, null);
    }

    private static string GetNodeTypeName(ushort rawType)
    {
        if (rawType < 1000)
            return Enum.IsDefined(typeof(NodeType), rawType)
                ? ((NodeType)rawType).ToString()
                : $"Node{rawType}";

        var componentRawType = (byte)(rawType - 1000);
        return Enum.IsDefined(typeof(ComponentType), componentRawType)
            ? ((ComponentType)componentRawType).ToString()
            : $"Component{componentRawType}";
    }

    private static string? TryReadNodeText(AtkResNode* node, ushort rawType)
    {
        try
        {
            if (rawType == (ushort)NodeType.Text)
            {
                var textNode = node->GetAsAtkTextNode();
                if (textNode != null)
                    return NormalizeNodeText(textNode->NodeText.ToString());
            }

            if (rawType == (ushort)NodeType.Counter)
            {
                var counterNode = node->GetAsAtkCounterNode();
                if (counterNode != null)
                    return NormalizeNodeText(counterNode->NodeText.ToString());
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? NormalizeNodeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return text
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
    }

    private static string FormatHalfVector2(HalfVector2 value)
        => $"({value.X}, {value.Y})";

    private static string FormatHalfVector4(HalfVector4 value)
        => $"({value.X}, {value.Y}, {value.Z}, {value.W})";

    private nint GetAddonAddress(string addonName)
    {
        var address = gameGui.GetAddonByName(addonName);
        if (address != nint.Zero)
            return address;

        return gameGui.GetAddonByName(addonName, 1);
    }

    private bool IsAddonVisible(string addonName)
        => GetAddonAddress(addonName) != nint.Zero;
}
