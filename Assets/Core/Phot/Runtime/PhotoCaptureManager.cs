using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Experimental.Rendering;

[RequireComponent(typeof(Camera))]
public class PhotoCaptureManager : MonoBehaviour
{
    public TrackPhotoRig rig;

    public bool useDynamicScale = true;
    public int outputWidth = 1920;
    public int outputHeight = 1080;

    public Texture2D lastPhoto;
    public System.Action<Texture2D> OnPhotoReady;

    RTHandle targetRT;
    Camera cam;
    CopyColorAfterPost copyPass;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (!rig) rig = GetComponent<TrackPhotoRig>();
    }

    void OnEnable()
    {
        EnsureTargetRT();
        FindCopyPass();
        if (rig != null) rig.OnShutter += HandleShutter;
    }

    void OnDisable()
    {
        if (rig != null) rig.OnShutter -= HandleShutter;
        if (copyPass != null) copyPass.ClearRequest();
        ReleaseTargetRT();
    }

    void OnDestroy()
    {
        ReleaseTargetRT();
    }

    void FindCopyPass()
    {
        if (copyPass != null) return;
        copyPass = CopyColorAfterPost.FindInstance();
        if (copyPass == null)
            Debug.LogWarning("[PhotoCaptureManager] 找不到 CopyColorAfterPost CustomPass，請確認場景中有 CustomPassVolume（Injection=After Post Process）。");
    }

    void EnsureTargetRT()
    {
        int w = cam ? cam.pixelWidth : outputWidth;
        int h = cam ? cam.pixelHeight : outputHeight;

        if (targetRT != null)
        {
            var rt = targetRT.rt;
            if (rt == null || rt.width != w || rt.height != h)
            {
                RTHandles.Release(targetRT);
                targetRT = null;
            }
        }
        if (targetRT != null) return;

        targetRT = RTHandles.Alloc(
            width: Mathf.Max(1, w),
            height: Mathf.Max(1, h),
            slices: 1,
            dimension: UnityEngine.Rendering.TextureDimension.Tex2D,
            colorFormat: GraphicsFormat.R8G8B8A8_UNorm,
            useDynamicScale: false,
            name: "PhotoCaptureRT_2D"
        );
    }

    void ReleaseTargetRT()
    {
        if (targetRT != null)
        {
            RTHandles.Release(targetRT);
            targetRT = null;
        }
    }

    void HandleShutter()
    {
        FindCopyPass();
        if (copyPass == null) return;

        EnsureTargetRT();
        if (targetRT == null) return;

        copyPass.RequestCapture(targetRT, cam, captureNextFrame: true);
        StartCoroutine(Readback());
    }

    IEnumerator Readback()
    {
        if (copyPass == null) yield break;

        int waitFrame = Time.frameCount + 1;
        int timeoutFrame = waitFrame + 30;

        while (copyPass.lastCapturedFrame < waitFrame)
        {
            if (Time.frameCount > timeoutFrame)
            {
                Debug.LogError("[PhotoCaptureManager] 擷取逾時，請確認 CustomPassVolume 存在且 Injection=After Post Process。");
                yield break;
            }
            yield return null;
        }

        yield return new WaitForEndOfFrame();

        var rt = targetRT != null ? targetRT.rt : null;
        if (rt == null) yield break;

        var req = AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32);
        while (!req.done) yield return null;
        if (req.hasError) yield break;

        if (lastPhoto == null || lastPhoto.width != rt.width || lastPhoto.height != rt.height)
            lastPhoto = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, false);

        lastPhoto.LoadRawTextureData(req.GetData<byte>());
        lastPhoto.Apply(false, false);
        lastPhoto.wrapMode = TextureWrapMode.Clamp;
        lastPhoto.filterMode = FilterMode.Bilinear;
        lastPhoto.anisoLevel = 0;

        OnPhotoReady?.Invoke(lastPhoto);
    }
}
