using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace footballer.Services;

public sealed unsafe class CharacterInspectFootwearService
{
    private const byte FeetSlotIndex = 4;
    private static readonly TimeSpan PendingTimeout = TimeSpan.FromSeconds(8);

    private readonly Configuration configuration;
    private readonly IPluginLog log;

    private uint pendingEntityId;
    private string pendingLabel = "current target";
    private DateTime pendingQueuedAtUtc;
    private uint lastAppliedEntityId;
    private ulong lastObservedFeetValue = ulong.MaxValue;
    private int pendingApplyAttempts;
    private string lastDiagnosticSignature = string.Empty;

    public CharacterInspectFootwearService(Configuration configuration, IPluginLog log)
    {
        this.configuration = configuration;
        this.log = log;
        LastStatus = configuration.WithoutFootwear
            ? "Barefoot preview mode is on. Use Inspect to queue a multi-seam feet-slot clear for the current CharacterInspect target."
            : "Barefoot preview mode is off.";
    }

    public string LastStatus { get; private set; }

    public void HandleModeChanged()
    {
        if (!configuration.WithoutFootwear)
        {
            ClearPending("Barefoot preview mode is off.");
            return;
        }

        LastStatus = "Barefoot preview mode is on. Use Inspect to queue a multi-seam feet-slot clear for the current CharacterInspect target.";
        QueueCurrentInspectIfPossible();
    }

    public void QueueForInspectRequest(uint entityId, string label)
    {
        if (!configuration.WithoutFootwear)
        {
            LastStatus = "Barefoot preview mode is off.";
            return;
        }

        if (entityId == 0)
        {
            LastStatus = "Cannot queue barefoot preview mode yet because the inspect target has no live entity id.";
            return;
        }

        pendingEntityId = entityId;
        pendingLabel = string.IsNullOrWhiteSpace(label) ? $"entity {FormatEntityId(entityId)}" : label;
        pendingQueuedAtUtc = DateTime.UtcNow;
        lastAppliedEntityId = 0;
        lastObservedFeetValue = ulong.MaxValue;
        pendingApplyAttempts = 0;
        lastDiagnosticSignature = string.Empty;
        LastStatus = $"Queued a barefoot preview update for {pendingLabel}. Waiting for CharacterInspect to finish loading that target.";
        LogDiagnostic("queued", default, LastStatus);
    }

    public void QueueCurrentInspectIfPossible()
    {
        if (!configuration.WithoutFootwear)
        {
            LastStatus = "Barefoot preview mode is off.";
            return;
        }

        var agent = AgentInspect.Instance();
        if (agent == null)
        {
            LastStatus = "CharacterInspect is unavailable right now, so barefoot preview mode cannot queue yet.";
            return;
        }

        var entityId = agent->CurrentEntityId != 0
            ? agent->CurrentEntityId
            : agent->RequestEntityId;
        if (entityId == 0)
        {
            LastStatus = "CharacterInspect has no current or pending target yet, so barefoot preview mode has nothing to queue.";
            return;
        }

        QueueForInspectRequest(entityId, $"entity {FormatEntityId(entityId)}");
    }

    public string? GetCaptureBlockReason(uint currentEntityId)
    {
        if (!configuration.WithoutFootwear)
            return null;

        if (currentEntityId == 0)
            return "CharacterInspect has no current entity yet, so barefoot preview capture cannot run.";

        if (lastAppliedEntityId == currentEntityId && lastObservedFeetValue == 0)
            return null;

        QueueCurrentInspectIfPossible();
        return "Barefoot preview is still being applied to the current CharacterInspect target. Wait a moment, then capture again.";
    }

    public void OnFrameworkUpdate()
    {
        if (!configuration.WithoutFootwear)
        {
            if (pendingEntityId != 0)
                ClearPending("Barefoot preview mode was turned off, so the pending feet-slot clear was cancelled.");
            else
                LastStatus = "Barefoot preview mode is off.";

            return;
        }

        if (pendingEntityId == 0)
            return;

        if (DateTime.UtcNow - pendingQueuedAtUtc > PendingTimeout)
        {
            var timeoutState = CaptureFeetState(null, null);
            LogDiagnostic("timeout", timeoutState, $"Barefoot preview update timed out for {pendingLabel}. Re-open Inspect and try again.");
            ClearPending($"Barefoot preview update timed out for {pendingLabel}. Re-open Inspect and try again.");
            return;
        }

        var agent = AgentInspect.Instance();
        if (agent == null)
            return;

        if (agent->CurrentEntityId != pendingEntityId)
        {
            LastStatus = $"Waiting for CharacterInspect to switch to {pendingLabel} before clearing the feet slot.";
            return;
        }

        var charaView = &agent->CharaView;
        if (!IsPreviewReady(charaView))
        {
            LastStatus = $"CharacterInspect is open for {pendingLabel}, but the preview model is not ready for a feet-slot clear yet.";
            return;
        }

        var previewCharacter = charaView->GetCharacter();
        if (previewCharacter == null)
        {
            LastStatus = $"CharacterInspect is open for {pendingLabel}, but the preview character is still unavailable.";
            return;
        }

        var currentState = CaptureFeetState(charaView, previewCharacter);
        lastAppliedEntityId = pendingEntityId;
        lastObservedFeetValue = currentState.HumanFeetValue;

        if (!currentState.HasAnyFeetData)
        {
            LastStatus = $"CharacterInspect is open for {pendingLabel}, but the preview model is not ready to expose a feet slot yet.";
            return;
        }

        if (currentState.IsBarefoot)
        {
            ClearPending($"Barefoot preview is ready for {pendingLabel}. {currentState.DescribeSuccess()}");
            return;
        }

        ApplyBarefootPreview(charaView, previewCharacter);
        pendingApplyAttempts++;

        var updatedState = CaptureFeetState(charaView, previewCharacter);
        lastObservedFeetValue = updatedState.HumanFeetValue;
        if (updatedState.IsBarefoot)
        {
            var successStatus = $"Barefoot preview applied for {pendingLabel}. {updatedState.DescribeSuccess()}";
            LogDiagnostic("success", updatedState, successStatus);
            ClearPending(successStatus);
            return;
        }

        LastStatus =
            $"Barefoot preview update attempt {pendingApplyAttempts} is still applying for {pendingLabel}. {updatedState.DescribePending()}";
        LogDiagnostic("pending", updatedState, LastStatus);
    }

    private void ClearPending(string status)
    {
        pendingEntityId = 0;
        pendingLabel = "current target";
        pendingQueuedAtUtc = DateTime.MinValue;
        pendingApplyAttempts = 0;
        LastStatus = status;
    }

    private static bool IsPreviewReady(AgentInspect.InspectCharaView* charaView)
        => charaView->CharacterLoaded ||
           charaView->CharacterDataCopied ||
           charaView->State != 0;

    private void LogDiagnostic(string stage, FeetPreviewState state, string status)
    {
        var signature = $"{stage}|{pendingEntityId:X8}|{state.GetSignature()}|{status}";
        if (signature == lastDiagnosticSignature)
            return;

        lastDiagnosticSignature = signature;
        log.Information(
            "[footballer] Barefoot preview {Stage} for {EntityId}: {Status} | {State}",
            stage,
            FormatEntityId(pendingEntityId),
            status,
            state.DescribeForLog());
    }

    private static void ApplyBarefootPreview(
        AgentInspect.InspectCharaView* charaView,
        Character* previewCharacter)
    {
        var slotIndex = (int)FeetSlotIndex;
        var emptyEquipment = default(EquipmentModelId);

        charaView->SetItemSlotData(FeetSlotIndex, 0u, 0, 0, 0u, false);

        var items = charaView->Items;
        if (items.Length > slotIndex)
        {
            ref var feetItem = ref items[slotIndex];
            feetItem.ItemId = 0;
            feetItem.GlamourItemId = 0;
            feetItem.Stain0Id = 0;
            feetItem.Stain1Id = 0;
            feetItem.GlamourStain0Id = 0;
            feetItem.GlamourStain1Id = 0;
            feetItem.ModelMain = 0;
            feetItem.ModelSub = 0;
        }

        var modelData = charaView->ModelData;
        var equipmentModelIds = modelData.EquipmentModelIds;
        if (equipmentModelIds.Length > slotIndex)
            equipmentModelIds[slotIndex] = emptyEquipment;

        charaView->SetModelData(&modelData);

        var equipmentSlot = (DrawDataContainer.EquipmentSlot)FeetSlotIndex;
        previewCharacter->DrawData.Equipment(equipmentSlot) = emptyEquipment;
        previewCharacter->DrawData.LoadEquipment(equipmentSlot, &emptyEquipment, true);

        var drawObject = previewCharacter->GameObject.DrawObject;
        if (drawObject != null)
        {
            var human = (Human*)drawObject;
            human->Feet = emptyEquipment;
            human->SlotNeedsUpdateBitfield |= 1u << FeetSlotIndex;
            human->SetEquipmentSlotModel(FeetSlotIndex, &emptyEquipment);
            human->LoadSlot(FeetSlotIndex);
        }

        charaView->Update(0, previewCharacter);
    }

    private static FeetPreviewState CaptureFeetState(
        AgentInspect.InspectCharaView* charaView,
        Character* previewCharacter)
    {
        var state = default(FeetPreviewState);
        var slotIndex = (int)FeetSlotIndex;

        if (charaView != null)
        {
            var items = charaView->Items;
            if (items.Length > slotIndex)
            {
                var item = items[slotIndex];
                state.HasCharaViewItem = true;
                state.ItemId = item.ItemId;
                state.GlamourItemId = item.GlamourItemId;
                state.ItemModelMain = item.ModelMain;
                state.ItemModelSub = item.ModelSub;
            }

            var equipmentModelIds = charaView->ModelData.EquipmentModelIds;
            if (equipmentModelIds.Length > slotIndex)
            {
                state.HasModelDataFeet = true;
                state.ModelDataFeetValue = equipmentModelIds[slotIndex].Value;
            }
        }

        if (previewCharacter != null)
        {
            var equipmentSlot = (DrawDataContainer.EquipmentSlot)FeetSlotIndex;
            state.DrawDataFeetValue = previewCharacter->DrawData.Equipment(equipmentSlot).Value;

            var drawObject = previewCharacter->GameObject.DrawObject;
            if (drawObject != null)
            {
                state.HasHumanFeet = true;
                var human = (Human*)drawObject;
                state.HumanFeetValue = human->Feet.Value;
            }
        }

        return state;
    }

    private static string FormatEntityId(uint entityId)
        => entityId == 0 ? "-" : $"0x{entityId:X8}";

    private struct FeetPreviewState
    {
        public bool HasCharaViewItem;
        public bool HasModelDataFeet;
        public bool HasHumanFeet;
        public uint ItemId;
        public uint GlamourItemId;
        public ulong ItemModelMain;
        public ulong ItemModelSub;
        public ulong ModelDataFeetValue;
        public ulong DrawDataFeetValue;
        public ulong HumanFeetValue;

        public bool HasAnyFeetData =>
            HasCharaViewItem || HasModelDataFeet || DrawDataFeetValue != 0 || HasHumanFeet;

        public bool IsBarefoot =>
            (!HasCharaViewItem || (ItemId == 0 && GlamourItemId == 0 && ItemModelMain == 0 && ItemModelSub == 0)) &&
            (!HasModelDataFeet || ModelDataFeetValue == 0) &&
            DrawDataFeetValue == 0 &&
            (!HasHumanFeet || HumanFeetValue == 0);

        public string GetSignature()
            => $"{ItemId:X8}:{GlamourItemId:X8}:{ItemModelMain:X}:{ItemModelSub:X}:{ModelDataFeetValue:X}:{DrawDataFeetValue:X}:{HumanFeetValue:X}";

        public string DescribeSuccess()
            => $"Feet cleared across the inspect item, model, draw-data, and live preview seams. {DescribeCore()}";

        public string DescribePending()
            => $"Current feet state: {DescribeCore()}";

        public string DescribeForLog()
            => DescribeCore();

        private string DescribeCore()
            => $"Item={FormatHex(ItemId)}, Glamour={FormatHex(GlamourItemId)}, ItemModel=({FormatHex(ItemModelMain)}, {FormatHex(ItemModelSub)}), ModelData={FormatHex(ModelDataFeetValue)}, DrawData={FormatHex(DrawDataFeetValue)}, Human={FormatHex(HumanFeetValue)}";
    }

    private static string FormatHex(uint value)
        => value == 0 ? "0" : $"0x{value:X8}";

    private static string FormatHex(ulong value)
        => value == 0 ? "0" : $"0x{value:X}";
}
