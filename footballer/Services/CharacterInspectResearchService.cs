using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Common.Math;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using footballer.Models;
using FfxivVector3 = FFXIVClientStructs.FFXIV.Common.Math.Vector3;
using SceneCamera = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Camera;
using SceneCameraManager = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CameraManager;

namespace footballer.Services;

public sealed unsafe class CharacterInspectResearchService
{
    private readonly IGameGui gameGui;

    public CharacterInspectResearchService(IGameGui gameGui)
    {
        this.gameGui = gameGui;
    }

    public CharacterInspectResearchSnapshot CaptureSnapshot()
    {
        var agent = AgentInspect.Instance();
        if (agent == null)
        {
            return new CharacterInspectResearchSnapshot(
                false,
                nint.Zero,
                false,
                nint.Zero,
                0f,
                0f,
                0,
                0,
                0,
                0,
                0,
                false,
                0,
                0,
                0,
                0f,
                nint.Zero,
                nint.Zero,
                "-",
                "-",
                "-",
                "-",
                "-",
                nint.Zero,
                -1,
                nint.Zero,
                nint.Zero,
                nint.Zero,
                "-",
                "-",
                "-",
                "-",
                "-",
                "Preview camera is unavailable because AgentInspect is not available yet.",
                false,
                false,
                nint.Zero,
                nint.Zero,
                0,
                nint.Zero,
                false,
                0f,
                0f,
                1f,
                1f,
                0,
                0,
                false,
                null,
                "CharacterInspect export capture is unavailable because AgentInspect is not available yet.",
                "AgentInspect is not available yet, so CharacterInspect research cannot run right now.",
                "Log into the game, then use Inspect on a party row or showcase card to open CharacterInspect for a live entity.");
        }

        var addonAddress = GetAddonAddress("CharacterInspect");
        var addonVisible = addonAddress != nint.Zero;
        var addonX = 0f;
        var addonY = 0f;
        nint previewComponentAddress = nint.Zero;
        nint collisionNodeAddress = nint.Zero;
        var previewCallbackBaseId = 0;
        nint previewNodeAddress = nint.Zero;
        var previewNodeVisible = false;
        var previewNodeX = 0f;
        var previewNodeY = 0f;
        var previewNodeScaleX = 1f;
        var previewNodeScaleY = 1f;
        ushort previewNodeWidth = 0;
        ushort previewNodeHeight = 0;

        if (addonVisible)
        {
            var addon = (AddonCharacterInspect*)addonAddress;
            addonX = addon->X;
            addonY = addon->Y;
            var previewComponent = addon->PreviewController.Component;
            previewComponentAddress = (nint)previewComponent;
            collisionNodeAddress = (nint)addon->PreviewController.CollisionNode;
            previewCallbackBaseId = addon->PreviewController.CallbackBaseId;

            if (previewComponent != null && previewComponent->OwnerNode != null)
            {
                var ownerNode = &previewComponent->OwnerNode->AtkResNode;
                previewNodeAddress = (nint)previewComponent->OwnerNode;
                previewNodeVisible = ownerNode->IsVisible();
                previewNodeX = ownerNode->X;
                previewNodeY = ownerNode->Y;
                previewNodeScaleX = ownerNode->ScaleX;
                previewNodeScaleY = ownerNode->ScaleY;
                previewNodeWidth = ownerNode->Width;
                previewNodeHeight = ownerNode->Height;
            }
        }

        var charaView = &agent->CharaView;
        var snapshotCapturedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var inspectStateReady = agent->CurrentEntityId != 0 &&
                                (charaView->CharacterLoaded ||
                                 charaView->CharacterDataCopied ||
                                 charaView->State != 0);
        var previewCharacterAddress = nint.Zero;
        var previewDrawObjectAddress = nint.Zero;
        var previewGameObjectPosition = "-";
        var previewGameObjectRotation = "-";
        var previewDrawObjectPosition = "-";
        var previewDrawObjectRotation = "-";
        TryCapturePreviewCharacterState(
            charaView,
            out previewCharacterAddress,
            out previewDrawObjectAddress,
            out previewGameObjectPosition,
            out previewGameObjectRotation,
            out previewDrawObjectPosition,
            out previewDrawObjectRotation);
        var cameraManagerAddress = nint.Zero;
        var cameraManagerIndex = -1;
        var rawSceneCameraAddress = nint.Zero;
        var managerCurrentCameraAddress = nint.Zero;
        var cameraAddress = nint.Zero;
        var cameraPosition = "-";
        var cameraLookAt = "-";
        var cameraRotation = "-";
        var cameraDistance = "-";
        var cameraFoV = "-";
        var cameraSnapshotStatus = "CharacterInspect preview camera pointer is not available yet.";
        TryCaptureInspectCamera(
            charaView,
            out cameraManagerAddress,
            out cameraManagerIndex,
            out rawSceneCameraAddress,
            out managerCurrentCameraAddress,
            out cameraAddress,
            out cameraPosition,
            out cameraLookAt,
            out cameraRotation,
            out cameraDistance,
            out cameraFoV,
            out cameraSnapshotStatus);
        var captureReady = addonVisible &&
                           previewNodeAddress != nint.Zero &&
                           previewNodeVisible &&
                           inspectStateReady;
        var activeExportPayload = (PortraitExportPayloadSnapshot?)null;
        var activeExportStatus = BuildInspectCaptureStatus(
            charaView,
            agent->CurrentEntityId,
            previewNodeAddress != nint.Zero,
            previewNodeVisible);

        var safeStatus = captureReady
            ? $"CharacterInspect is open for entity 0x{agent->CurrentEntityId:X8}. The live preview node is visible at {previewNodeWidth}x{previewNodeHeight}, the inspect-side state is stable enough for controlled lower-biased preview snipping from a down-extended source region, and read-only preview camera state capture can run."
            : addonVisible
                ? $"CharacterInspect is open for entity 0x{agent->CurrentEntityId:X8}. Inspect-side CharaView state is {charaView->State}, loaded={(charaView->CharacterLoaded ? "Y" : "N")}, copied={(charaView->CharacterDataCopied ? "Y" : "N")}."
            : agent->CurrentEntityId != 0 || agent->RequestEntityId != 0
                ? $"CharacterInspect is not visible right now, but AgentInspect still has request/current entity state ({agent->RequestEntityId:X8} -> {agent->CurrentEntityId:X8})."
                : "CharacterInspect is not open right now. Use Inspect on a party row or showcase card to drive the direct character-derived capture path.";
        var nextStep = captureReady
            ? "Press SNIP PREVIEW while CharacterInspect is stable, keep the footballer window away from the preview area, confirm the lower-biased saved image from the down-extended source region lands on the matching showcase card, and use COPY INSPECT REPORT to capture the current preview camera pose."
            : addonVisible
                ? "Keep CharacterInspect open until the preview node becomes visible and the inspect-side state stabilizes, then use SNIP PREVIEW once the bounds look correct."
            : "Use Inspect on a party row or showcase card, then re-open this section and confirm CharacterInspect plus the inspect-side CharaView fields populate.";

        return new CharacterInspectResearchSnapshot(
            true,
            (nint)agent,
            addonVisible,
            addonAddress,
            addonX,
            addonY,
            agent->RequestEntityId,
            agent->CurrentEntityId,
            agent->FetchCharacterDataStatus,
            agent->FetchSearchCommentStatus,
            agent->FetchFreeCompanyStatus,
            agent->IsBuddyInspect,
            charaView->State,
            charaView->ClientObjectId,
            charaView->ClientObjectIndex,
            charaView->ZoomRatio,
            previewCharacterAddress,
            previewDrawObjectAddress,
            previewGameObjectPosition,
            previewGameObjectRotation,
            previewDrawObjectPosition,
            previewDrawObjectRotation,
            snapshotCapturedAt,
            cameraManagerAddress,
            cameraManagerIndex,
            rawSceneCameraAddress,
            managerCurrentCameraAddress,
            cameraAddress,
            cameraPosition,
            cameraLookAt,
            cameraRotation,
            cameraDistance,
            cameraFoV,
            cameraSnapshotStatus,
            charaView->CharacterLoaded,
            charaView->CharacterDataCopied,
            previewComponentAddress,
            collisionNodeAddress,
            previewCallbackBaseId,
            previewNodeAddress,
            previewNodeVisible,
            previewNodeX,
            previewNodeY,
            previewNodeScaleX,
            previewNodeScaleY,
            previewNodeWidth,
            previewNodeHeight,
            captureReady,
            activeExportPayload,
            activeExportStatus,
            safeStatus,
            nextStep);
    }

    private static void TryCapturePreviewCharacterState(
        AgentInspect.InspectCharaView* charaView,
        out nint previewCharacterAddress,
        out nint previewDrawObjectAddress,
        out string previewGameObjectPosition,
        out string previewGameObjectRotation,
        out string previewDrawObjectPosition,
        out string previewDrawObjectRotation)
    {
        previewCharacterAddress = nint.Zero;
        previewDrawObjectAddress = nint.Zero;
        previewGameObjectPosition = "-";
        previewGameObjectRotation = "-";
        previewDrawObjectPosition = "-";
        previewDrawObjectRotation = "-";

        var previewCharacter = charaView->GetCharacter();
        if (previewCharacter == null)
            return;

        previewCharacterAddress = (nint)previewCharacter;
        previewGameObjectPosition = FormatVector3(previewCharacter->GameObject.Position);
        previewGameObjectRotation = FormatRotationFloat(previewCharacter->GameObject.Rotation);

        var drawObject = previewCharacter->GameObject.DrawObject;
        if (drawObject == null)
            return;

        previewDrawObjectAddress = (nint)drawObject;
        previewDrawObjectPosition = FormatVector3(drawObject->Object.Position);
        previewDrawObjectRotation = FormatQuaternion(drawObject->Object.Rotation);
    }

    private static string BuildInspectCaptureStatus(
        AgentInspect.InspectCharaView* charaView,
        uint currentEntityId,
        bool previewNodeAvailable,
        bool previewNodeVisible)
    {
        if (currentEntityId == 0)
            return "CharacterInspect has no current entity yet, so inspect capture is waiting on the inspect target.";

        if (!previewNodeAvailable)
            return "CharacterInspect is open, but the preview widget node is not available yet.";

        if (!previewNodeVisible)
            return "CharacterInspect preview widget exists, but it is not visible yet.";

        if (!charaView->CharacterLoaded &&
            !charaView->CharacterDataCopied &&
            charaView->State == 0)
        {
            return "CharacterInspect preview is visible, but the inspect-side model state does not look initialized enough for controlled capture yet.";
        }

        return $"CharacterInspect preview is visible and the inspect-side state is stable for entity 0x{currentEntityId:X8}. Raw lower-biased preview snipping can use a down-extended source region from these bounds directly, and read-only preview camera state capture can inspect the current camera pose.";
    }

    private static void TryCaptureInspectCamera(
        AgentInspect.InspectCharaView* charaView,
        out nint cameraManagerAddress,
        out int cameraManagerIndex,
        out nint rawSceneCameraAddress,
        out nint managerCurrentCameraAddress,
        out nint cameraAddress,
        out string cameraPosition,
        out string cameraLookAt,
        out string cameraRotation,
        out string cameraDistance,
        out string cameraFoV,
        out string cameraSnapshotStatus)
    {
        cameraManagerAddress = (nint)charaView->CameraManager;
        cameraManagerIndex = -1;
        rawSceneCameraAddress = (nint)charaView->Camera;
        managerCurrentCameraAddress = nint.Zero;
        cameraAddress = nint.Zero;
        cameraPosition = "-";
        cameraLookAt = "-";
        cameraRotation = "-";
        cameraDistance = "-";
        cameraFoV = "-";
        cameraSnapshotStatus = "CharacterInspect preview camera pointer is not available yet.";

        if (charaView->CameraManager != nint.Zero)
        {
            var cameraManager = (SceneCameraManager*)charaView->CameraManager;
            cameraManagerIndex = cameraManager->CameraIndex;
            if (cameraManagerIndex is >= 0 and < 14)
            {
                var currentCamera = (*cameraManager).CurrentCamera;
                managerCurrentCameraAddress = (nint)currentCamera;
                if (currentCamera != null)
                {
                    PopulateCameraState(
                        currentCamera,
                        out cameraAddress,
                        out cameraPosition,
                        out cameraLookAt,
                        out cameraRotation,
                        out cameraDistance,
                        out cameraFoV);
                    cameraSnapshotStatus =
                        $"Read-only preview camera state captured from CharacterInspect scene camera manager index {cameraManagerIndex}.";
                    return;
                }

                cameraSnapshotStatus =
                    $"CharacterInspect scene camera manager is present, but index {cameraManagerIndex} did not resolve to a current scene camera.";
            }
            else
            {
                cameraSnapshotStatus =
                    $"CharacterInspect scene camera manager is present, but camera index {cameraManagerIndex} is outside the expected 0-13 range.";
            }
        }

        if (charaView->Camera == null)
            return;

        PopulateCameraState(
            charaView->Camera,
            out cameraAddress,
            out cameraPosition,
            out cameraLookAt,
            out cameraRotation,
            out cameraDistance,
            out cameraFoV);
        cameraSnapshotStatus = cameraManagerAddress != nint.Zero
            ? $"Fell back to the raw CharacterInspect scene camera because the scene camera manager path did not yield a live current camera (index {cameraManagerIndex})."
            : "Read-only preview camera state captured from the raw CharacterInspect scene camera.";
    }

    private static void PopulateCameraState(
        SceneCamera* camera,
        out nint cameraAddress,
        out string cameraPosition,
        out string cameraLookAt,
        out string cameraRotation,
        out string cameraDistance,
        out string cameraFoV)
    {
        cameraAddress = (nint)camera;
        var position = camera->Object.Position;
        var lookAt = camera->LookAtVector;
        cameraPosition = FormatVector3(position);
        cameraLookAt = FormatVector3(lookAt);
        cameraRotation = FormatDerivedYawPitch(position, lookAt);
        cameraDistance = FormatDistance(position, lookAt);
        cameraFoV = camera->RenderCamera != null &&
                    float.IsFinite(camera->RenderCamera->FoV) &&
                    camera->RenderCamera->FoV > 0f
            ? camera->RenderCamera->FoV.ToString("0.###")
            : "-";
    }

    private static string FormatHalfVector4(HalfVector4 value)
        => $"{value.X:0.###}, {value.Y:0.###}, {value.Z:0.###}, {value.W:0.###}";

    private static string FormatHalfVector2(HalfVector2 value)
        => $"{value.X:0.###}, {value.Y:0.###}";

    private static string FormatVector3(FfxivVector3 value)
        => $"{value.X:0.###}, {value.Y:0.###}, {value.Z:0.###}";

    private static string FormatQuaternion(Quaternion value)
        => $"{value.X:0.###}, {value.Y:0.###}, {value.Z:0.###}, {value.W:0.###}";

    private static string FormatRotationFloat(float value)
        => $"{value:0.###} rad / {(value * (180f / MathF.PI)):0.###} deg";

    private static string FormatDistance(FfxivVector3 position, FfxivVector3 lookAt)
        => FfxivVector3.Distance(position, lookAt).ToString("0.###");

    private static string FormatDerivedYawPitch(FfxivVector3 position, FfxivVector3 lookAt)
    {
        var direction = lookAt - position;
        var horizontalLength = MathF.Sqrt((direction.X * direction.X) + (direction.Z * direction.Z));
        if (horizontalLength < 0.0001f && MathF.Abs(direction.Y) < 0.0001f)
            return "Unavailable";

        var yawDegrees = MathF.Atan2(direction.X, direction.Z) * (180f / MathF.PI);
        var pitchDegrees = MathF.Atan2(direction.Y, horizontalLength) * (180f / MathF.PI);
        return $"Yaw {yawDegrees:0.###}, Pitch {pitchDegrees:0.###}";
    }

    private nint GetAddonAddress(string addonName)
    {
        var address = gameGui.GetAddonByName(addonName);
        if (address != nint.Zero)
            return address;

        return gameGui.GetAddonByName(addonName, 1);
    }
}
