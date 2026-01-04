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

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (!rig) rig = GetComponent<TrackPhotoRig>();
    }

    void OnEnable()
    {
        EnsureTargetRT();
        if (rig != null) rig.OnShutter += HandleShutter;
    }

    void OnDisable()
    {
        if (rig != null) rig.OnShutter -= HandleShutter;
        CopyColorAfterPost.ClearRequest();
        ReleaseTargetRT();
    }

    void OnDestroy()
    {
        ReleaseTargetRT();
    }

    void EnsureTargetRT()
    {
        // 若解析度改變則重建
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

        // 關掉動態縮放，明確建 2D 紋理，避免建成 XR Array
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
        EnsureTargetRT();
        if (targetRT == null) return;

        // 固定使用下一幀擷取，與畫面所見一致
        CopyColorAfterPost.RequestCapture(targetRT, cam, captureNextFrame: true);
        StartCoroutine(Readback());
    }

    IEnumerator Readback()
    {
        int waitFrame = Time.frameCount + 1; // 你目前固定延一幀擷取
        int timeoutFrame = waitFrame + 30;

        while (CopyColorAfterPost.lastCapturedFrame < waitFrame)
        {
            if (Time.frameCount > timeoutFrame)
            {
                Debug.LogError("[PhotoCaptureManager] Capture timeout. 請確認 CustomPassVolume 是否存在且作用在同一台相機，以及 Injection=After Post Process。");
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
