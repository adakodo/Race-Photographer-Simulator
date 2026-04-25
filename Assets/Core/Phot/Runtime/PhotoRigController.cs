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
    public KeyCode subjectNextKey = KeyCode.Tab;
    public KeyCode subjectPrevKey = KeyCode.BackQuote;

    [Header("Steps")]
    public float shutterStepEV = 1f / 3f;
    public float apertureStepEV = 1f / 3f;
    public float isoStepEV = 1f / 3f;
    public float exposureCompStep = 0.3f;
    public float ndStep = 0.3f;
    public float focusStepMeters = 0.5f;
    public float focalStepMm = 5f;

    Camera _cam;
    int subjectIndex = -1;

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

        if (Input.GetKeyDown(ecDownKey)) rig.SetExposureComp(rig.exposureCompEV - exposureCompStep);
        if (Input.GetKeyDown(ecUpKey)) rig.SetExposureComp(rig.exposureCompEV + exposureCompStep);

        if (Input.GetKeyDown(ndDownKey)) rig.SetNDStops(rig.ndStops - ndStep);
        if (Input.GetKeyDown(ndUpKey)) rig.SetNDStops(rig.ndStops + ndStep);

        if (Input.GetKeyDown(afToggleKey)) rig.SetAFContinuous(!rig.afContinuous);
        if (Input.GetKey(focusNearKey)) rig.ManualFocusAdjust(-focusStepMeters * Time.deltaTime * 6f);
        if (Input.GetKey(focusFarKey)) rig.ManualFocusAdjust(focusStepMeters * Time.deltaTime * 6f);

        if (Input.GetKeyDown(driveCycleKey)) CycleDrive();
        if (Input.GetKeyDown(panAssistToggleKey)) rig.SetPanAssist(!rig.panAssist);
        if (Input.GetKeyDown(rollingToggleKey)) rig.SetRollingShutter(!rig.rollingShutterApprox);
        if (Input.GetKeyDown(shakeToggleKey)) rig.SetHandheldShake(!rig.handheldShake);

        if (Input.GetKeyDown(lensAKey)) rig.SwitchLens(rig.lensA, true);
        if (Input.GetKeyDown(lensBKey)) rig.SwitchLens(rig.lensB, true);

        if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f)
            rig.SetTargetFocal(rig.focalLengthMm + -Input.mouseScrollDelta.y * focalStepMm);

        // 追焦目標切換
        if (Input.GetKeyDown(subjectNextKey)) CycleSubject(1);
        if (Input.GetKeyDown(subjectPrevKey)) CycleSubject(-1);

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
        rig.SetShutterSpeed(rig.shutterSpeedSec * Mathf.Pow(2f, -ev));
    }

    void AdjustAperture(float ev)
    {
        rig.SetAperture(rig.apertureF * Mathf.Pow(2f, ev * 0.5f));
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

    void CycleSubject(int dir)
    {
        var mgr = BikeAITrafficManager.Instance;
        if (mgr == null) return;
        var agents = mgr.RankedAgents;
        if (agents == null || agents.Count == 0) return;

        subjectIndex += dir;
        if (subjectIndex < 0) subjectIndex = agents.Count - 1;
        if (subjectIndex >= agents.Count) subjectIndex = 0;

        rig.SetSubject(agents[subjectIndex].transform);
    }

    void CycleDrive()
    {
        switch (rig.driveMode)
        {
            case TrackPhotoRig.Drive.Single: rig.SetDriveMode(TrackPhotoRig.Drive.ContinuousLow); break;
            case TrackPhotoRig.Drive.ContinuousLow: rig.SetDriveMode(TrackPhotoRig.Drive.ContinuousHigh); break;
            case TrackPhotoRig.Drive.ContinuousHigh: rig.SetDriveMode(TrackPhotoRig.Drive.Custom); break;
            default: rig.SetDriveMode(TrackPhotoRig.Drive.Single); break;
        }
    }

}
