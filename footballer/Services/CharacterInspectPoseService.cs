using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Common.Math;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using SceneObject = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Object;

namespace footballer.Services;

public sealed unsafe class CharacterInspectPoseService
{
    private const float DesiredPreviewYawRadians = -1.26f;
    private static readonly Quaternion DesiredPreviewDrawRotation = BuildYawQuaternion(DesiredPreviewYawRadians);
    private static readonly TimeSpan PendingTimeout = TimeSpan.FromSeconds(8);
    private const float RotationTolerance = 0.01f;

    private readonly IPluginLog log;

    private uint pendingEntityId;
    private string pendingLabel = "current target";
    private DateTime pendingQueuedAtUtc;
    private uint lastAppliedEntityId;
    private string lastDiagnosticSignature = string.Empty;

    public CharacterInspectPoseService(IPluginLog log)
    {
        this.log = log;
        LastStatus = $"Inspect pose preset is idle. Target yaw is {FormatRotation(DesiredPreviewYawRadians)}.";
    }

    public string LastStatus { get; private set; }

    public void QueueForInspectRequest(uint entityId, string label)
    {
        if (entityId == 0)
        {
            LastStatus = "Cannot queue the inspect pose preset yet because the inspect target has no live entity id.";
            return;
        }

        pendingEntityId = entityId;
        pendingLabel = string.IsNullOrWhiteSpace(label) ? $"entity {FormatEntityId(entityId)}" : label;
        pendingQueuedAtUtc = DateTime.UtcNow;
        lastAppliedEntityId = 0;
        lastDiagnosticSignature = string.Empty;
        LastStatus = $"Queued the inspect pose preset for {pendingLabel}. Waiting for CharacterInspect to finish loading that target.";
        LogDiagnostic("queued", default, LastStatus);
    }

    public string? GetCaptureBlockReason(uint currentEntityId)
    {
        if (currentEntityId == 0)
            return "CharacterInspect has no current entity yet, so the inspect pose preset cannot run.";

        if (pendingEntityId == currentEntityId)
            return "The inspect pose preset is still being applied to the current CharacterInspect target. Wait a moment, then capture again.";

        return null;
    }

    public void OnFrameworkUpdate()
    {
        if (pendingEntityId == 0)
            return;

        if (DateTime.UtcNow - pendingQueuedAtUtc > PendingTimeout)
        {
            LogDiagnostic("timeout", default, $"Inspect pose preset timed out for {pendingLabel}. Re-open Inspect and try again.");
            ClearPending($"Inspect pose preset timed out for {pendingLabel}. Re-open Inspect and try again.");
            return;
        }

        var agent = AgentInspect.Instance();
        if (agent == null)
            return;

        if (agent->CurrentEntityId != pendingEntityId)
        {
            LastStatus = $"Waiting for CharacterInspect to switch to {pendingLabel} before applying the inspect pose preset.";
            return;
        }

        var charaView = &agent->CharaView;
        if (!IsPreviewReady(charaView))
        {
            LastStatus = $"CharacterInspect is open for {pendingLabel}, but the preview model is not ready for the inspect pose preset yet.";
            return;
        }

        var previewCharacter = charaView->GetCharacter();
        if (previewCharacter == null || previewCharacter->GameObject.DrawObject == null)
        {
            LastStatus = $"CharacterInspect is open for {pendingLabel}, but the preview character is still unavailable.";
            return;
        }

        var currentState = CapturePoseState(previewCharacter);
        if (currentState.IsAtPreset)
        {
            lastAppliedEntityId = pendingEntityId;
            var successStatus = $"Inspect pose preset is ready for {pendingLabel}. {currentState.DescribeSuccess()}";
            LogDiagnostic("ready", currentState, successStatus);
            ClearPending(successStatus);
            return;
        }

        ApplyPosePreset(previewCharacter);
        var updatedState = CapturePoseState(previewCharacter);
        if (updatedState.IsAtPreset)
        {
            lastAppliedEntityId = pendingEntityId;
            var successStatus = $"Applied the inspect pose preset for {pendingLabel}. {updatedState.DescribeSuccess()}";
            LogDiagnostic("success", updatedState, successStatus);
            ClearPending(successStatus);
            return;
        }

        LastStatus = $"Inspect pose preset is still applying for {pendingLabel}. {updatedState.DescribePending()}";
        LogDiagnostic("pending", updatedState, LastStatus);
    }

    private void ClearPending(string status)
    {
        pendingEntityId = 0;
        pendingLabel = "current target";
        pendingQueuedAtUtc = DateTime.MinValue;
        LastStatus = status;
    }

    private void LogDiagnostic(string stage, PoseState state, string status)
    {
        var signature = $"{stage}|{pendingEntityId:X8}|{state.GetSignature()}|{status}";
        if (signature == lastDiagnosticSignature)
            return;

        lastDiagnosticSignature = signature;
        log.Information(
            "[footballer] Inspect pose {Stage} for {EntityId}: {Status} | {State}",
            stage,
            FormatEntityId(pendingEntityId),
            status,
            state.DescribeForLog());
    }

    private static void ApplyPosePreset(Character* previewCharacter)
    {
        previewCharacter->GameObject.Rotation = DesiredPreviewYawRadians;
        var drawObject = previewCharacter->GameObject.DrawObject;
        if (drawObject == null)
            return;

        drawObject->Object.Rotation = DesiredPreviewDrawRotation;
    }

    private static PoseState CapturePoseState(Character* previewCharacter)
    {
        var state = default(PoseState);
        state.GameYawRadians = previewCharacter->GameObject.Rotation;

        var drawObject = previewCharacter->GameObject.DrawObject;
        if (drawObject != null)
        {
            state.DrawRotation = drawObject->Object.Rotation;
            state.HasDrawRotation = true;
        }

        return state;
    }

    private static bool IsPreviewReady(AgentInspect.InspectCharaView* charaView)
        => charaView->CharacterLoaded ||
           charaView->CharacterDataCopied ||
           charaView->State != 0;

    private static Quaternion BuildYawQuaternion(float yawRadians)
    {
        var halfYaw = yawRadians * 0.5f;
        return new Quaternion
        {
            X = 0f,
            Y = MathF.Sin(halfYaw),
            Z = 0f,
            W = MathF.Cos(halfYaw),
        };
    }

    private static bool Approximately(float left, float right)
        => MathF.Abs(left - right) <= RotationTolerance;

    private static bool Approximately(Quaternion left, Quaternion right)
        => Approximately(left.X, right.X) &&
           Approximately(left.Y, right.Y) &&
           Approximately(left.Z, right.Z) &&
           Approximately(left.W, right.W);

    private static string FormatEntityId(uint entityId)
        => entityId == 0 ? "-" : $"0x{entityId:X8}";

    private static string FormatRotation(float value)
        => $"{value:0.###} rad / {(value * (180f / MathF.PI)):0.###} deg";

    private static string FormatQuaternion(Quaternion value)
        => $"{value.X:0.###}, {value.Y:0.###}, {value.Z:0.###}, {value.W:0.###}";

    private struct PoseState
    {
        public float GameYawRadians;
        public Quaternion DrawRotation;
        public bool HasDrawRotation;

        public bool IsAtPreset =>
            Approximately(GameYawRadians, DesiredPreviewYawRadians) &&
            HasDrawRotation &&
            Approximately(DrawRotation, DesiredPreviewDrawRotation);

        public string GetSignature()
            => $"{GameYawRadians:0.###}:{DrawRotation.X:0.###}:{DrawRotation.Y:0.###}:{DrawRotation.Z:0.###}:{DrawRotation.W:0.###}:{HasDrawRotation}";

        public string DescribeSuccess()
            => $"Preview game yaw is {FormatRotation(GameYawRadians)} and draw rotation is ({FormatQuaternion(DrawRotation)}).";

        public string DescribePending()
            => $"Current preview yaw is {FormatRotation(GameYawRadians)} and draw rotation is ({FormatQuaternion(DrawRotation)}). Target draw rotation is ({FormatQuaternion(DesiredPreviewDrawRotation)}).";

        public string DescribeForLog()
            => $"GameYaw={FormatRotation(GameYawRadians)}, DrawRotation=({FormatQuaternion(DrawRotation)}), HasDrawRotation={(HasDrawRotation ? "Y" : "N")}";
    }
}
