using UnityEngine;

/// <summary>
/// IMGUI G-force meter（G-ball），顯示當前追蹤目標的縱向/側向 G 值。
/// 從 TrackPhotoRig.subject 上的 BikeAITrackAgent 讀取 GForce 資料。
/// </summary>
public class GForceMeterHUD : MonoBehaviour
{
    [Header("資料來源")]
    public TrackPhotoRig rig;

    [Header("位置與大小")]
    [Tooltip("圓心距右下角的偏移（像素）")]
    public Vector2 cornerOffset = new Vector2(100f, 180f);
    public float meterRadius = 60f;

    [Header("刻度")]
    [Tooltip("G-ball 滿刻度對應的 G 值")]
    public float maxG = 2f;
    [Tooltip("繪製的同心圓數量")]
    public int ringCount = 2;

    [Header("外觀")]
    public Color bgColor = new Color(0f, 0f, 0f, 0.5f);
    public Color ringColor = new Color(1f, 1f, 1f, 0.15f);
    public Color crossColor = new Color(1f, 1f, 1f, 0.1f);
    public Color dotColor = new Color(1f, 0.3f, 0.2f, 1f);
    public float dotRadius = 6f;
    public Color textColor = new Color(1f, 1f, 1f, 0.6f);
    public int fontSize = 11;

    [Header("軌跡")]
    [Tooltip("軌跡點數量，0 = 不畫軌跡")]
    public int trailLength = 30;
    public Color trailColor = new Color(1f, 0.3f, 0.2f, 0.3f);

    // 預生成的 Texture
    Texture2D _circleTex;
    Texture2D _ringTex;
    Texture2D _lineTex;
    int _texSize = 64;

    Vector2[] _trail;
    int _trailIndex;
    bool _trailFilled;

    Texture2D CircleTex
    {
        get
        {
            if (_circleTex == null)
                _circleTex = GenerateCircleTexture(_texSize, true);
            return _circleTex;
        }
    }

    Texture2D RingTex
    {
        get
        {
            if (_ringTex == null)
                _ringTex = GenerateCircleTexture(_texSize, false);
            return _ringTex;
        }
    }

    Texture2D LineTex
    {
        get
        {
            if (_lineTex == null)
            {
                _lineTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _lineTex.hideFlags = HideFlags.HideAndDontSave;
                _lineTex.SetPixel(0, 0, Color.white);
                _lineTex.Apply();
            }
            return _lineTex;
        }
    }

    Texture2D GenerateCircleTexture(int size, bool filled)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.HideAndDontSave;
        float center = size * 0.5f;
        float outerR = center - 1f;
        float innerR = outerR - 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (filled)
                {
                    float a = Mathf.Clamp01(outerR - dist + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                else
                {
                    float outer = Mathf.Clamp01(outerR - dist + 0.5f);
                    float inner = Mathf.Clamp01(dist - innerR + 0.5f);
                    float a = outer * inner;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
        }
        tex.Apply();
        return tex;
    }

    void OnGUI()
    {
        if (rig == null || rig.subject == null) return;
        if (Event.current.type != EventType.Repaint) return;

        BikeAITrackAgent agent = rig.subject.GetComponentInParent<BikeAITrackAgent>();
        if (agent == null) return;

        Vector2 g = agent.GForce;
        Vector2 center = new Vector2(Screen.width - cornerOffset.x, Screen.height - cornerOffset.y);

        // 背景圓
        DrawCircle(center, meterRadius + 4f, bgColor, true);

        // 同心圓刻度
        for (int i = 1; i <= ringCount; i++)
        {
            float r = meterRadius * ((float)i / ringCount);
            DrawCircle(center, r, ringColor, false);
        }

        // 十字線
        DrawLine(new Vector2(center.x - meterRadius, center.y),
                 new Vector2(center.x + meterRadius, center.y), crossColor, 1f);
        DrawLine(new Vector2(center.x, center.y - meterRadius),
                 new Vector2(center.x, center.y + meterRadius), crossColor, 1f);

        // G 值映射到像素偏移（X = 側向右正，Y = 縱向上正 → 螢幕 Y 反轉）
        float px = Mathf.Clamp(g.x / maxG, -1f, 1f) * meterRadius;
        float py = Mathf.Clamp(-g.y / maxG, -1f, 1f) * meterRadius;
        Vector2 dotPos = center + new Vector2(px, py);

        // 軌跡
        if (trailLength > 0)
        {
            if (_trail == null || _trail.Length != trailLength)
            {
                _trail = new Vector2[trailLength];
                _trailIndex = 0;
                _trailFilled = false;
            }
            _trail[_trailIndex] = dotPos;
            _trailIndex = (_trailIndex + 1) % trailLength;
            if (_trailIndex == 0) _trailFilled = true;

            int count = _trailFilled ? trailLength : _trailIndex;
            for (int i = 0; i < count; i++)
            {
                float alpha = (float)i / count;
                Color c = trailColor;
                c.a *= alpha * 0.5f;
                DrawCircle(_trail[(_trailIndex - count + i + trailLength) % trailLength], 2f, c, true);
            }
        }

        // G 點
        DrawCircle(dotPos, dotRadius, dotColor, true);

        // 數值文字
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = textColor;

        float totalG = g.magnitude;
        Rect labelRect = new Rect(center.x - meterRadius, center.y + meterRadius + 6f, meterRadius * 2f, 18f);
        GUI.Label(labelRect, totalG.ToString("0.00") + " G", style);

        // 軸標籤
        style.fontSize = fontSize - 2;
        style.normal.textColor = new Color(textColor.r, textColor.g, textColor.b, 0.4f);
        Rect brakeLabel = new Rect(center.x - 20f, center.y - meterRadius - 16f, 40f, 14f);
        GUI.Label(brakeLabel, "BRK", style);
        Rect accelLabel = new Rect(center.x - 20f, center.y + meterRadius + 22f, 40f, 14f);
        GUI.Label(accelLabel, "ACC", style);
    }

    // --- 繪製工具 ---

    void DrawCircle(Vector2 center, float radius, Color color, bool filled)
    {
        Color oc = GUI.color;
        GUI.color = color;
        float size = radius * 2f;
        Rect rect = new Rect(center.x - radius, center.y - radius, size, size);
        GUI.DrawTexture(rect, filled ? CircleTex : RingTex);
        GUI.color = oc;
    }

    void DrawLine(Vector2 a, Vector2 b, Color color, float width)
    {
        Color oc = GUI.color;
        GUI.color = color;

        Vector2 d = b - a;
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        float length = d.magnitude;

        Matrix4x4 saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, a);
        GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, length, width), LineTex);
        GUI.matrix = saved;

        GUI.color = oc;
    }
}
