using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class CopyColorAfterPost : CustomPass
{
    static RTHandle pendingRT;
    static Camera pendingCam;
    static bool pending;
    static int targetFrame;
    public static int lastCapturedFrame { get; private set; } = -1;

    public static void RequestCapture(RTHandle rt, Camera cam, bool captureNextFrame)
    {
        pendingRT = rt;
        pendingCam = cam;
        pending = true;
        targetFrame = Time.frameCount + (captureNextFrame ? 1 : 0);
        lastCapturedFrame = -1;
    }

    public static void ClearRequest()
    {
        pending = false;
        pendingCam = null;
        pendingRT = null;
        lastCapturedFrame = -1;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        pending = false;
        pendingCam = null;
        pendingRT = null;
        lastCapturedFrame = -1;
    }

    protected override void Execute(CustomPassContext ctx)
    {
        
        //Debug.Log($"[CopyColorAfterPost] Exec on {ctx.hdCamera.camera.name}, pending={pending}, tgtFrame={targetFrame}, now={Time.frameCount}");

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
}
