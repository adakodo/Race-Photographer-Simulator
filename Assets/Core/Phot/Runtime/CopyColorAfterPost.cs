using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class CopyColorAfterPost : CustomPass
{
    RTHandle pendingRT;
    Camera pendingCam;
    bool pending;
    int targetFrame;
    public int lastCapturedFrame { get; private set; } = -1;

    public void RequestCapture(RTHandle rt, Camera cam, bool captureNextFrame)
    {
        pendingRT = rt;
        pendingCam = cam;
        pending = true;
        targetFrame = Time.frameCount + (captureNextFrame ? 1 : 0);
        lastCapturedFrame = -1;
    }

    public void ClearRequest()
    {
        pending = false;
        pendingCam = null;
        pendingRT = null;
        lastCapturedFrame = -1;
    }

    protected override void Execute(CustomPassContext ctx)
    {
        if (!pending || pendingRT == null) return;
        if (ctx.hdCamera.camera != pendingCam) return;
        if (Time.frameCount < targetFrame) return;

        CoreUtils.SetRenderTarget(ctx.cmd, pendingRT);
        CoreUtils.ClearRenderTarget(ctx.cmd, ClearFlag.Color, Color.clear);
        HDUtils.BlitCameraTexture(ctx.cmd, ctx.cameraColorBuffer, pendingRT);

        lastCapturedFrame = Time.frameCount;
        pending = false;
        pendingCam = null;
    }

    public static CopyColorAfterPost FindInstance()
    {
        var volumes = Object.FindObjectsByType<CustomPassVolume>(FindObjectsSortMode.None);
        foreach (var vol in volumes)
        {
            if (vol.customPasses == null) continue;
            foreach (var pass in vol.customPasses)
            {
                if (pass is CopyColorAfterPost cpp)
                    return cpp;
            }
        }
        return null;
    }
}
