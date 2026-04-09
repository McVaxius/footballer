using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using footballer.Models;

namespace footballer.Services;

public sealed unsafe class PartyFeetRefreshService
{
    private static readonly TimeSpan MemberTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan InspectRetryInterval = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan CaptureReadyDwell = TimeSpan.FromSeconds(2);

    private readonly IPluginLog log;
    private readonly CharacterInspectResearchService characterInspectResearchService;
    private readonly CharacterInspectPreviewCaptureService previewCaptureService;
    private readonly CharacterInspectPoseService poseService;
    private readonly CharacterInspectFootwearService footwearService;
    private readonly Action<string> statusPrinter;
    private readonly Func<PartyShowcaseMember, string> memberLabelFormatter;

    private readonly Queue<PartyShowcaseMember> pendingMembers = new();

    private PartyShowcaseMember? currentMember;
    private DateTime currentMemberStartedAtUtc;
    private DateTime lastInspectRequestAtUtc;
    private int totalMembers;
    private int completedMembers;
    private int failedMembers;
    private int skippedMembers;
    private bool completionAnnounced;
    private DateTime captureReadySinceUtc;
    private string lastLoggedStatus = string.Empty;

    public PartyFeetRefreshService(
        IPluginLog log,
        CharacterInspectResearchService characterInspectResearchService,
        CharacterInspectPreviewCaptureService previewCaptureService,
        CharacterInspectPoseService poseService,
        CharacterInspectFootwearService footwearService,
        Action<string> statusPrinter,
        Func<PartyShowcaseMember, string> memberLabelFormatter)
    {
        this.log = log;
        this.characterInspectResearchService = characterInspectResearchService;
        this.previewCaptureService = previewCaptureService;
        this.poseService = poseService;
        this.footwearService = footwearService;
        this.statusPrinter = statusPrinter;
        this.memberLabelFormatter = memberLabelFormatter;
        LastStatus = "No automatic party feet refresh is queued.";
    }

    public string LastStatus { get; private set; }

    public bool IsActive => currentMember is not null || pendingMembers.Count > 0;

    public (int QueuedCount, int SkippedCount) QueueRefresh(IReadOnlyList<PartyShowcaseMember> members)
    {
        pendingMembers.Clear();
        currentMember = null;
        totalMembers = 0;
        completedMembers = 0;
        failedMembers = 0;
        skippedMembers = 0;
        completionAnnounced = false;
        captureReadySinceUtc = DateTime.MinValue;
        lastLoggedStatus = string.Empty;
        currentMemberStartedAtUtc = DateTime.MinValue;
        lastInspectRequestAtUtc = DateTime.MinValue;

        foreach (var member in members)
        {
            if (member.EntityId == 0)
            {
                skippedMembers++;
                continue;
            }

            pendingMembers.Enqueue(member);
        }

        totalMembers = pendingMembers.Count;
        LastStatus = totalMembers == 0
            ? "No live party members are available for an automatic feet refresh."
            : $"Queued automatic party feet refresh for {totalMembers} member(s).";
        LogStatus("queued", LastStatus);
        return (totalMembers, skippedMembers);
    }

    public void OnFrameworkUpdate()
    {
        if (currentMember is null)
        {
            if (pendingMembers.Count == 0)
            {
                AnnounceCompletionIfNeeded();
                return;
            }

            BeginNextMember();
            return;
        }

        var member = currentMember;
        if (member is null)
            return;

        if (DateTime.UtcNow - currentMemberStartedAtUtc > MemberTimeout)
        {
            failedMembers++;
            AdvanceAfterAttempt($"Timed out while refreshing {FormatMemberLabel(member)}.");
            return;
        }

        var agent = AgentInspect.Instance();
        if (agent == null)
        {
            LastStatus = $"Waiting for AgentInspect before refreshing {FormatMemberLabel(member)} ({GetCurrentOrdinal()}/{totalMembers}).";
            return;
        }

        if (agent->CurrentEntityId != member.EntityId)
        {
            captureReadySinceUtc = DateTime.MinValue;
            if (DateTime.UtcNow - lastInspectRequestAtUtc >= InspectRetryInterval)
                RequestInspect(member, agent);
            else
                LastStatus = $"Waiting for CharacterInspect to switch to {FormatMemberLabel(member)} ({GetCurrentOrdinal()}/{totalMembers}).";

            return;
        }

        var snapshot = characterInspectResearchService.CaptureSnapshot();
        if (snapshot.CurrentEntityId != member.EntityId)
        {
            captureReadySinceUtc = DateTime.MinValue;
            LastStatus = $"CharacterInspect is still settling on {FormatMemberLabel(member)} ({GetCurrentOrdinal()}/{totalMembers}).";
            return;
        }

        if (!snapshot.CaptureReady)
        {
            captureReadySinceUtc = DateTime.MinValue;
            LastStatus = $"Waiting for the inspect preview to become capture-ready for {FormatMemberLabel(member)} ({GetCurrentOrdinal()}/{totalMembers}).";
            return;
        }

        var poseBlockReason = poseService.GetCaptureBlockReason(snapshot.CurrentEntityId);
        if (!string.IsNullOrWhiteSpace(poseBlockReason))
        {
            captureReadySinceUtc = DateTime.MinValue;
            LastStatus = $"Waiting for the preset inspect pose on {FormatMemberLabel(member)} ({GetCurrentOrdinal()}/{totalMembers}).";
            return;
        }

        var barefootBlockReason = footwearService.GetCaptureBlockReason(snapshot.CurrentEntityId);
        if (!string.IsNullOrWhiteSpace(barefootBlockReason))
        {
            captureReadySinceUtc = DateTime.MinValue;
            LastStatus = $"Waiting for the barefoot preview apply path on {FormatMemberLabel(member)} ({GetCurrentOrdinal()}/{totalMembers}).";
            return;
        }

        if (captureReadySinceUtc == DateTime.MinValue)
            captureReadySinceUtc = DateTime.UtcNow;

        var stableFor = DateTime.UtcNow - captureReadySinceUtc;
        if (stableFor < CaptureReadyDwell)
        {
            var remaining = CaptureReadyDwell - stableFor;
            LastStatus = $"Holding {FormatMemberLabel(member)} steady for capture ({GetCurrentOrdinal()}/{totalMembers}). Waiting {remaining.TotalSeconds:0.0}s more before saving the feet preview.";
            return;
        }

        var captureResult = previewCaptureService.Capture(snapshot, member.CharacterKey, member.EntityId);
        if (captureResult.Success)
        {
            completedMembers++;
            AdvanceAfterAttempt($"Updated the feet preview for {FormatMemberLabel(member)} ({completedMembers}/{totalMembers}).");
            return;
        }

        failedMembers++;
        AdvanceAfterAttempt($"Capture failed for {FormatMemberLabel(member)}: {captureResult.Status}");
    }

    private void BeginNextMember()
    {
        currentMember = pendingMembers.Dequeue();
        currentMemberStartedAtUtc = DateTime.UtcNow;
        lastInspectRequestAtUtc = DateTime.MinValue;
        captureReadySinceUtc = DateTime.MinValue;
        completionAnnounced = false;

        var member = currentMember;
        if (member is null)
            return;

        var agent = AgentInspect.Instance();
        if (agent != null)
            RequestInspect(member, agent);
        else
            LastStatus = $"Waiting for AgentInspect before starting {FormatMemberLabel(member)} ({GetCurrentOrdinal()}/{totalMembers}).";
    }

    private void RequestInspect(PartyShowcaseMember member, AgentInspect* agent)
    {
        var memberLabel = FormatMemberLabel(member);
        agent->ExamineCharacter(member.EntityId);
        poseService.QueueForInspectRequest(member.EntityId, memberLabel);
        footwearService.QueueForInspectRequest(member.EntityId, memberLabel);
        lastInspectRequestAtUtc = DateTime.UtcNow;
        LastStatus = $"Refreshing feet preview for {FormatMemberLabel(member)} ({GetCurrentOrdinal()}/{totalMembers}). Requested CharacterInspect.";
        LogStatus("inspect", LastStatus);
    }

    private void AdvanceAfterAttempt(string status)
    {
        LastStatus = status;
        LogStatus("advance", status);
        currentMember = null;
        currentMemberStartedAtUtc = DateTime.MinValue;
        lastInspectRequestAtUtc = DateTime.MinValue;
        captureReadySinceUtc = DateTime.MinValue;
        AnnounceCompletionIfNeeded();
    }

    private void AnnounceCompletionIfNeeded()
    {
        if (completionAnnounced || pendingMembers.Count > 0 || currentMember is not null)
            return;

        completionAnnounced = true;
        var summary = $"Automatic party feet refresh finished. Captured {completedMembers}/{totalMembers} member(s)";
        if (failedMembers > 0)
            summary += $", failed {failedMembers}";
        if (skippedMembers > 0)
            summary += $", skipped {skippedMembers} without live entity ids";

        summary += ".";
        LastStatus = summary;
        LogStatus("complete", summary);
        statusPrinter(summary);
    }

    private void LogStatus(string stage, string status)
    {
        var signature = $"{stage}|{status}";
        if (signature == lastLoggedStatus)
            return;

        lastLoggedStatus = signature;
        log.Information("[footballer] Party feet refresh {Stage}: {Status}", stage, status);
    }

    private int GetCurrentOrdinal()
        => totalMembers - pendingMembers.Count;

    private string FormatMemberLabel(PartyShowcaseMember member)
        => memberLabelFormatter(member);
}
