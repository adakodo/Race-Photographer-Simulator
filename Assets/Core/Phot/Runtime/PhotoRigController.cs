using UnityEngine;

public class PhotoRigController : MonoBehaviour
{
    public TrackPhotoRig rig;

    [Header("模式切換")]
    public ViewfinderOverlay viewfinder;
    public TrackPhotoHUD photoHUD;
    public PhotoGallery gallery;
    [Tooltip("第一人稱模式的固定視野角度")]
    public float firstPersonFOV = 70f;

    [Header("Keys")]
    public KeyCode shutterDownKey = KeyCode.Alpha1;
    public KeyCode shutterUpKey = KeyCode.Alpha2;
    public KeyCode apertureDownKey = KeyCode.Alpha3;
    public KeyCode apertureUpKey = KeyCode.Alpha4;
    public KeyCode ecDownKey = KeyCode.Q;
    public KeyCode ecUpKey = KeyCode.E;
    public KeyCode lensAKey = KeyCode.F1;
    public KeyCode lensBKey = KeyCode.F2;

    [Header("Steps")]
    public float shutterStepEV = 1f / 3f;
    public float apertureStepEV = 1f / 3f;
    public float exposureCompStep = 0.3f;
    public float focalStepMm = 5f;

    [Header("半按對焦")]
    [Tooltip("半按對焦的 Raycast 最大距離")]
    public float focusRayMaxDist = 500f;

    /// <summary>是否處於攝影模式</summary>
    public bool CameraMode { get; private set; }

    /// <summary>是否正在半按快門（對焦中）</summary>
    public bool IsHalfPress { get; private set; }

    Camera _cam;

    void Reset()
    {
        if (!rig) rig = GetComponent<TrackPhotoRig>();
    }

    void Start()
    {
        if (!rig) rig = GetComponent<TrackPhotoRig>();
        _cam = GetComponent<Camera>();
        SetCameraMode(false);
    }

    void Update()
    {
        if (!rig) return;

        // 圖庫開啟時不處理任何操作
        if (gallery != null && gallery.IsOpen) return;

        // 右鍵切換模式
        if (Input.GetMouseButtonDown(1))
        {
            SetCameraMode(!CameraMode);
            return;
        }

        if (!CameraMode) return;

        // 半按快門：按住左鍵 = 對焦，放開 = 拍照
        if (Input.GetMouseButtonDown(0))
        {
            IsHalfPress = true;
            HalfPressFocus();
        }
        if (Input.GetMouseButton(0) && IsHalfPress)
        {
            HalfPressFocus();
        }
        if (Input.GetMouseButtonUp(0) && IsHalfPress)
        {
            IsHalfPress = false;
            rig.TakeOneShot();
        }

        if (Input.GetKeyDown(shutterDownKey)) AdjustShutter(-shutterStepEV);
        if (Input.GetKeyDown(shutterUpKey)) AdjustShutter(+shutterStepEV);

        if (Input.GetKeyDown(apertureDownKey)) AdjustAperture(+apertureStepEV);
        if (Input.GetKeyDown(apertureUpKey)) AdjustAperture(-apertureStepEV);

        if (Input.GetKeyDown(ecDownKey)) rig.SetExposureComp(rig.exposureCompEV - exposureCompStep);
        if (Input.GetKeyDown(ecUpKey)) rig.SetExposureComp(rig.exposureCompEV + exposureCompStep);

        if (Input.GetKeyDown(lensAKey)) rig.SwitchLens(rig.lensA, true);
        if (Input.GetKeyDown(lensBKey)) rig.SwitchLens(rig.lensB, true);

        if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
            rig.SetTargetFocal(rig.focalLengthMm + -Input.mouseScrollDelta.y * focalStepMm);

    }

    void HalfPressFocus()
    {
        if (_cam == null) return;
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        rig.FocusAtScreenPoint(center, focusRayMaxDist);
    }

    void SetCameraMode(bool on)
    {
        CameraMode = on;
        if (viewfinder != null) viewfinder.overlayEnabled = on;
        if (photoHUD != null) photoHUD.showHUD = on;

        if (rig != null)
        {
            rig.enabled = on;
            rig.SetDOFActive(on);
            // 停用 AF-C 和 Pan Assist，對焦完全靠半按快門
            rig.SetAFContinuous(false);
            rig.SetPanAssist(false);
        }

        if (!on && _cam != null)
        {
            _cam.usePhysicalProperties = false;
            _cam.fieldOfView = firstPersonFOV;
            if (rig != null) rig.ResetExposureToAuto();
        }
    }

    void AdjustShutter(float ev)
    {
        rig.SetShutterSpeed(rig.shutterSpeedSec * Mathf.Pow(2f, -ev));
    }

    void AdjustAperture(float ev)
    {
        rig.SetAperture(rig.apertureF * Mathf.Pow(2f, ev * 0.5f));
    }

}
