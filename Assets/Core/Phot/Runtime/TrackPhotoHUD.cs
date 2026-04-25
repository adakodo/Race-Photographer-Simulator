using UnityEngine;

public class TrackPhotoHUD : MonoBehaviour
{
    public TrackPhotoRig rig;
    public bool showHUD = true;

    [Header("Style")]
    public int fontSize = 24;
    public float bottomBarHeight = 44f;
    public float sidePadding = 16f;
    public Color barColor = new Color(0f, 0f, 0f, 0.75f);
    public Color textColor = Color.white;

    Texture2D _tex1x1;
    Texture2D Tex1x1()
    {
        if (_tex1x1 == null)
        {
            _tex1x1 = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _tex1x1.hideFlags = HideFlags.HideAndDontSave;
            _tex1x1.SetPixel(0, 0, Color.white);
            _tex1x1.Apply();
        }
        return _tex1x1;
    }

    void OnGUI()
    {
        if (!showHUD || rig == null) return;
        if (Event.current.type != EventType.Repaint) return;

        Rect bar = new Rect(0, Screen.height - bottomBarHeight, Screen.width, bottomBarHeight);
        Color oc = GUI.color;
        GUI.color = barColor;
        GUI.DrawTexture(bar, Tex1x1());
        GUI.color = oc;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = fontSize;
        style.normal.textColor = textColor;

        string sShutter = FormatShutter(rig.shutterSpeedSec);
        string sAperture = "f/" + FormatAperture(rig.apertureF);
        string sComp = FormatExposureComp(rig.exposureCompEV);
        string sFocal = Mathf.RoundToInt(rig.focalLengthMm) + "mm";

        float w = bar.width / 4f;

        Rect r1 = new Rect(bar.x + sidePadding, bar.y, w - sidePadding * 2f, bar.height);
        Rect r2 = new Rect(bar.x + w, bar.y, w, bar.height);
        Rect r3 = new Rect(bar.x + w * 2f, bar.y, w, bar.height);
        Rect r4 = new Rect(bar.x + w * 3f, bar.y, w - sidePadding, bar.height);

        GUIStyle left = new GUIStyle(style) { alignment = TextAnchor.MiddleLeft };
        GUIStyle center = new GUIStyle(style) { alignment = TextAnchor.MiddleCenter };
        GUIStyle right = new GUIStyle(style) { alignment = TextAnchor.MiddleRight };

        GUI.Label(r1, sShutter, left);
        GUI.Label(r2, sAperture, center);
        GUI.Label(r3, sComp, center);
        GUI.Label(r4, sFocal, right);
    }

    string FormatShutter(float sec)
    {
        if (sec < 1f)
        {
            int denom = Mathf.Max(1, Mathf.RoundToInt(1f / Mathf.Max(sec, 1e-6f)));
            return "1/" + denom;
        }
        if (sec < 10f) return sec.ToString("0.0") + "s";
        return Mathf.RoundToInt(sec) + "s";
    }

    string FormatAperture(float f)
    {
        if (Mathf.Abs(f - Mathf.Round(f)) < 0.05f) return Mathf.Round(f).ToString("0");
        return f.ToString("0.0");
    }

    string FormatExposureComp(float ev)
    {
        if (Mathf.Abs(ev) < 0.05f) return "+0.0";
        return (ev > 0 ? "+" : "") + ev.ToString("0.0");
    }
}