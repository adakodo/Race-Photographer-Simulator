using UnityEngine;

public class ControlsHint : MonoBehaviour
{
    public GameManager gm;

    [Header("顯示設定")]
    public float showDuration = 12f;
    public KeyCode toggleKey = KeyCode.F2;

    float hideTime;
    bool forceHidden;

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

    bool initialized;

    void Update()
    {
        if (gm == null) return;

        if (!initialized && gm.CurrentState == GameManager.State.Playing)
        {
            hideTime = Time.time + showDuration;
            initialized = true;
        }

        if (Input.GetKeyDown(toggleKey))
            forceHidden = !forceHidden;
    }

    void OnGUI()
    {
        if (gm == null) return;
        if (Event.current.type != EventType.Repaint) return;

        var state = gm.CurrentState;
        if (state != GameManager.State.Playing) return;
        if (forceHidden) return;

        float alpha = 1f;
        if (state == GameManager.State.Playing)
        {
            float remaining = hideTime - Time.time;
            if (remaining <= 0f) return;
            alpha = Mathf.Clamp01(remaining / 2f);
        }

        DrawHints(alpha);
    }

    void DrawHints(float alpha)
    {
        float panelW = 280f;
        float panelH = 330f;
        float x = 16f;
        float y = Screen.height - panelH - 60f;

        // 背景
        Color oc = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f * alpha);
        GUI.DrawTexture(new Rect(x, y, panelW, panelH), Tex1x1());
        GUI.color = oc;

        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        title.normal.textColor = new Color(1f, 0.9f, 0.3f, alpha);

        GUIStyle line = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleLeft
        };
        line.normal.textColor = new Color(1f, 1f, 1f, 0.85f * alpha);

        GUIStyle dim = new GUIStyle(line);
        dim.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 0.6f * alpha);

        float lx = x + 12f;
        float ly = y + 8f;
        float lh = 22f;

        GUI.Label(new Rect(lx, ly, panelW, lh), "CONTROLS", title); ly += lh + 4;

        DrawEntry(lx, ly, panelW, lh, "WASD", "移動", line); ly += lh;
        DrawEntry(lx, ly, panelW, lh, "Mouse", "視角", line); ly += lh;
        DrawEntry(lx, ly, panelW, lh, "Shift / Ctrl", "加速 / 減速", line); ly += lh;
        DrawEntry(lx, ly, panelW, lh, "Space", "跳躍", line); ly += lh;
        ly += 6;

        DrawEntry(lx, ly, panelW, lh, "Right Click", "舉起 / 放下相機", line); ly += lh;
        DrawEntry(lx, ly, panelW, lh, "Hold Left", "對焦 → 放開拍照", line); ly += lh;
        ly += 6;

        DrawEntry(lx, ly, panelW, lh, "Scroll", "變焦（攝影模式）", line); ly += lh;
        DrawEntry(lx, ly, panelW, lh, "1 / 2", "切換鏡頭", line); ly += lh;
        DrawEntry(lx, ly, panelW, lh, "[ / ]", "快門速度", line); ly += lh;
        ly += 6;

        DrawEntry(lx, ly, panelW, lh, "F1", "照片圖庫", line); ly += lh;
        DrawEntry(lx, ly, panelW, lh, "F2", "顯示/隱藏提示", dim); ly += lh;
    }

    void DrawEntry(float x, float y, float w, float h, string key, string desc, GUIStyle style)
    {
        GUI.Label(new Rect(x, y, 100, h), key, style);
        GUI.Label(new Rect(x + 110, y, w - 120, h), desc, style);
    }
}
