using UnityEngine;

public class PhotoRigController : MonoBehaviour
{
    public TrackPhotoRig rig;

    [Header("Keys")]
    public KeyCode singleShotKey = KeyCode.Return;
    public KeyCode holdToggleKey = KeyCode.Space;
    public KeyCode shutterDownKey = KeyCode.LeftBracket;
    public KeyCode shutterUpKey = KeyCode.RightBracket;
    public KeyCode apertureDownKey = KeyCode.Semicolon;
    public KeyCode apertureUpKey = KeyCode.Quote;
    public KeyCode isoDownKey = KeyCode.Minus;
    public KeyCode isoUpKey = KeyCode.Equals;
    public KeyCode ecDownKey = KeyCode.Comma;
    public KeyCode ecUpKey = KeyCode.Period;
    public KeyCode ndDownKey = KeyCode.N;
    public KeyCode ndUpKey = KeyCode.M;
    public KeyCode afToggleKey = KeyCode.R;
    public KeyCode focusNearKey = KeyCode.F;
    public KeyCode focusFarKey = KeyCode.G;
    public KeyCode driveCycleKey = KeyCode.V;
    public KeyCode panAssistToggleKey = KeyCode.P;
    public KeyCode rollingToggleKey = KeyCode.T;
    public KeyCode shakeToggleKey = KeyCode.H;
    public KeyCode lensAKey = KeyCode.Alpha1;
    public KeyCode lensBKey = KeyCode.Alpha2;

    [Header("Steps")]
    public float shutterStepEV = 1f / 3f;
    public float apertureStepEV = 1f / 3f;
    public float isoStepEV = 1f / 3f;
    public float exposureCompStep = 0.3f;
    public float ndStep = 0.3f;
    public float focusStepMeters = 0.5f;
    public float focalStepMm = 5f;

    Camera _cam;

    void Reset()
    {
        if (!rig) rig = GetComponent<TrackPhotoRig>();
    }

    void Start()
    {
        if (!rig) rig = GetComponent<TrackPhotoRig>();
        _cam = GetComponent<Camera>();
    }

    void Update()
    {
        if (!rig) return;

        if (Input.GetKeyDown(singleShotKey)) rig.TakeOneShot();

        if (Input.GetKeyDown(holdToggleKey))
        {
            if (!rig.HoldShutter) rig.StartHoldShutter();
            else rig.StopHoldShutter();
        }

        if (Input.GetKeyDown(shutterDownKey)) AdjustShutter(-shutterStepEV);
        if (Input.GetKeyDown(shutterUpKey)) AdjustShutter(+shutterStepEV);

        if (Input.GetKeyDown(apertureDownKey)) AdjustAperture(+apertureStepEV);
        if (Input.GetKeyDown(apertureUpKey)) AdjustAperture(-apertureStepEV);

        if (Input.GetKeyDown(isoDownKey)) AdjustISO(-isoStepEV);
        if (Input.GetKeyDown(isoUpKey)) AdjustISO(+isoStepEV);

        if (Input.GetKeyDown(ecDownKey)) rig.exposureCompEV -= exposureCompStep;
        if (Input.GetKeyDown(ecUpKey)) rig.exposureCompEV += exposureCompStep;

        if (Input.GetKeyDown(ndDownKey)) rig.ndStops = Mathf.Max(0f, rig.ndStops - ndStep);
        if (Input.GetKeyDown(ndUpKey)) rig.ndStops = Mathf.Min(8f, rig.ndStops + ndStep);

        if (Input.GetKeyDown(afToggleKey)) rig.afContinuous = !rig.afContinuous;
        if (Input.GetKey(focusNearKey)) rig.focusDistanceM = Mathf.Max(0.1f, rig.focusDistanceM - focusStepMeters * Time.deltaTime * 6f);
        if (Input.GetKey(focusFarKey)) rig.focusDistanceM += focusStepMeters * Time.deltaTime * 6f;

        if (Input.GetKeyDown(driveCycleKey)) CycleDrive();
        if (Input.GetKeyDown(panAssistToggleKey)) rig.panAssist = !rig.panAssist;
        if (Input.GetKeyDown(rollingToggleKey)) rig.rollingShutterApprox = !rig.rollingShutterApprox;
        if (Input.GetKeyDown(shakeToggleKey)) rig.handheldShake = !rig.handheldShake;

        if (Input.GetKeyDown(lensAKey)) TrySwitchLensA();
        if (Input.GetKeyDown(lensBKey)) TrySwitchLensB();

        if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
            rig.focalLengthMm = Mathf.Clamp(rig.focalLengthMm + -Input.mouseScrollDelta.y * focalStepMm, 8f, 600f);

        // Focus shortcuts
        if (Input.GetMouseButtonDown(2))
        {
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            rig.FocusAtScreenPoint(center);
        }
        if (Input.GetMouseButtonDown(0) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
        {
            rig.FocusAtScreenPoint(Input.mousePosition);
        }
    }

    void AdjustShutter(float ev)
    {
        float t = Mathf.Clamp(rig.shutterSpeedSec * Mathf.Pow(2f, -ev), 1f / 8000f, 30f);
        rig.shutterSpeedSec = t;
    }

    void AdjustAperture(float ev)
    {
        float n = Mathf.Clamp(rig.apertureF * Mathf.Pow(2f, ev * 0.5f), 0.7f, 32f);
        rig.apertureF = n;
    }

    void AdjustISO(float ev)
    {
        rig.shutterPriority = false;
        int isoCur = Mathf.Max(1, rig.GetISO());
        int isoNext = Mathf.Clamp(Mathf.RoundToInt(isoCur * Mathf.Pow(2f, ev)), Mathf.Max(50, rig.isoMin), rig.isoMax);
        rig.SetISOManual(NearestISO(isoNext));
    }

    int NearestISO(int iso)
    {
        int[] table = { 100, 125, 160, 200, 250, 320, 400, 500, 640, 800, 1000, 1250, 1600, 2000, 2500, 3200, 4000, 5000, 6400 };
        int best = table[0];
        int dbest = Mathf.Abs(iso - best);
        for (int i = 1; i < table.Length; ++i)
        {
            int d = Mathf.Abs(iso - table[i]);
            if (d < dbest) { dbest = d; best = table[i]; }
        }
        return best;
    }

    void CycleDrive()
    {
        switch (rig.driveMode)
        {
            case TrackPhotoRig.Drive.Single: rig.driveMode = TrackPhotoRig.Drive.ContinuousLow; break;
            case TrackPhotoRig.Drive.ContinuousLow: rig.driveMode = TrackPhotoRig.Drive.ContinuousHigh; break;
            case TrackPhotoRig.Drive.ContinuousHigh: rig.driveMode = TrackPhotoRig.Drive.Custom; break;
            default: rig.driveMode = TrackPhotoRig.Drive.Single; break;
        }
    }

    void TrySwitchLensA()
    {
        var m = typeof(TrackPhotoRig).GetMethod("SwitchLens", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var f = typeof(TrackPhotoRig).GetField("lensA", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (m != null && f != null) m.Invoke(rig, new object[] { f.GetValue(rig), true });
    }
    void TrySwitchLensB()
    {
        var m = typeof(TrackPhotoRig).GetMethod("SwitchLens", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var f = typeof(TrackPhotoRig).GetField("lensB", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (m != null && f != null) m.Invoke(rig, new object[] { f.GetValue(rig), true });
    }
}
