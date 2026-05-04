using UnityEngine;

public class TrackPhotoHUD : MonoBehaviour
{
    public TrackPhotoRig rig;
    public bool showHUD = true;

    [Header("Style")]
    public int valueFontSize = 20;
    public int labelFontSize = 11;
    public float bottomBarHeight = 64f;
    public float sidePadding = 16f;
    public Color barColor = new Color(0f, 0f, 0f, 0.75f);
    public Color valueColor = Color.white;
    public Color labelColor = new Color(1f, 1f, 1f, 0.5f);
    public Color warningColor = new Color(1f, 0.3f, 0.2f, 1f);

    [Header("曝光警告")]
    public bool showExposureWarning = true;
    public int warningFontSize = 16;

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

        float w = bar.width / 5f;

        DrawParam(new Rect(bar.x + sidePadding, bar.y, w - sidePadding, bar.height),
            "快門速度", FormatShutter(rig.shutterSpeedSec), "1 / 2", TextAnchor.MiddleLeft, valueColor);

        DrawParam(new Rect(bar.x + w, bar.y, w, bar.height),
            "光圈", "f/" + FormatAperture(rig.apertureF), "3 / 4", TextAnchor.MiddleCenter, valueColor);

        // ISO（自動，顯示撞限警告）
        int iso = rig.GetISO();
        bool isoAtLimit = (iso <= rig.isoMin || iso >= rig.isoMax);
        Color isoColor = isoAtLimit ? warningColor : valueColor;
        DrawParam(new Rect(bar.x + w * 2f, bar.y, w, bar.height),
            "ISO", "ISO " + iso, "AUTO", TextAnchor.MiddleCenter, isoColor);

        DrawParam(new Rect(bar.x + w * 3f, bar.y, w, bar.height),
            "曝光補償", FormatExposureComp(rig.exposureCompEV), "Q / E", TextAnchor.MiddleCenter, valueColor);

        DrawParam(new Rect(bar.x + w * 4f, bar.y, w - sidePadding, bar.height),
            "焦距", Mathf.RoundToInt(rig.focalLengthMm) + "mm", "Scroll", TextAnchor.MiddleRight, valueColor);

        // 過曝/欠曝警告
        if (showExposureWarning && isoAtLimit)
            DrawExposureWarning(bar, iso);
    }

    void DrawExposureWarning(Rect bar, int iso)
    {
        string msg;
        if (iso >= rig.isoMax)
            msg = "! 欠曝 — ISO 已達上限";
        else
            msg = "! 過曝 — ISO 已達下限";

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = warningFontSize,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = warningColor;

        // 閃爍效果
        float alpha = 0.6f + Mathf.Sin(Time.unscaledTime * 5f) * 0.4f;
        style.normal.textColor = new Color(warningColor.r, warningColor.g, warningColor.b, alpha);

        Rect warningRect = new Rect(0, bar.y - 30f, Screen.width, 24f);
        GUI.Label(warningRect, msg, style);
    }

    void DrawParam(Rect area, string label, string value, string key, TextAnchor align, Color valColor)
    {
        // 標籤（上方）
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = labelFontSize,
            alignment = align
        };
        labelStyle.normal.textColor = labelColor;

        Rect labelRect = new Rect(area.x, area.y + 4f, area.width, 16f);
        GUI.Label(labelRect, label, labelStyle);

        // 數值（中間）
        GUIStyle valueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = valueFontSize,
            alignment = align
        };
        valueStyle.normal.textColor = valColor;

        Rect valueRect = new Rect(area.x, area.y + 20f, area.width, 28f);
        GUI.Label(valueRect, value, valueStyle);

        // 按鍵提示（下方）
        if (!string.IsNullOrEmpty(key))
        {
            GUIStyle keyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = labelFontSize,
                alignment = align
            };
            keyStyle.normal.textColor = new Color(1f, 0.9f, 0.3f, 0.6f);

            Rect keyRect = new Rect(area.x, area.y + 48f, area.width, 16f);
            GUI.Label(keyRect, key, keyStyle);
        }
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
