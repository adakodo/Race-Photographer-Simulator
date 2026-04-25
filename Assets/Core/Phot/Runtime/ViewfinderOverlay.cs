using UnityEngine;

public class ViewfinderOverlay : MonoBehaviour
{
    public TrackPhotoRig rig;

    [Header("Viewfinder Overlay")]
    public bool overlayEnabled = true;
    public bool ruleOfThirds = true;
    public bool centerCross = true;
    public bool horizonLevel = true;
    public bool safeFrame = true;
    public float safeFrameAspect = 2.39f;
    public float overlayThickness = 2f;
    public Color overlayColor = new Color(1f, 1f, 1f, 0.8f);

    Texture2D _tex1x1;

    void Reset()
    {
        if (!rig) rig = GetComponent<TrackPhotoRig>();
    }

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
        if (!overlayEnabled || Event.current.type != EventType.Repaint) return;

        var r = new Rect(0, 0, Screen.width, Screen.height);
        DrawOverlay(r);
    }

    void DrawOverlay(Rect r)
    {
        if (ruleOfThirds)
        {
            float x1 = r.width / 3f;
            float x2 = x1 * 2f;
            float y1 = r.height / 3f;
            float y2 = y1 * 2f;
            DrawLine(new Vector2(x1, 0), new Vector2(x1, r.height), overlayColor, overlayThickness);
            DrawLine(new Vector2(x2, 0), new Vector2(x2, r.height), overlayColor, overlayThickness);
            DrawLine(new Vector2(0, y1), new Vector2(r.width, y1), overlayColor, overlayThickness);
            DrawLine(new Vector2(0, y2), new Vector2(r.width, y2), overlayColor, overlayThickness);
        }

        if (centerCross)
        {
            float cx = r.width * 0.5f;
            float cy = r.height * 0.5f;
            float len = Mathf.Min(r.width, r.height) * 0.02f;
            DrawLine(new Vector2(cx - len, cy), new Vector2(cx + len, cy), overlayColor, overlayThickness);
            DrawLine(new Vector2(cx, cy - len), new Vector2(cx, cy + len), overlayColor, overlayThickness);
        }

        if (horizonLevel)
        {
            float cy = r.height * 0.5f;
            DrawLine(new Vector2(r.width * 0.1f, cy), new Vector2(r.width * 0.9f, cy), overlayColor, overlayThickness);
        }

        if (safeFrame && safeFrameAspect > 0f)
        {
            float targetW = r.width;
            float targetH = targetW / safeFrameAspect;
            if (targetH > r.height)
            {
                targetH = r.height;
                targetW = targetH * safeFrameAspect;
            }
            float x = (r.width - targetW) * 0.5f;
            float y = (r.height - targetH) * 0.5f;

            DrawRect(new Rect(x, y, targetW, targetH), overlayColor, overlayThickness);
        }

        // 反光鏡遮黑
        if (rig != null && Time.time < rig.MirrorBlackoutUntil)
        {
            DrawFilledRect(r, new Color(0f, 0f, 0f, 0.6f));
        }
    }

    void DrawLine(Vector2 a, Vector2 b, Color c, float thickness)
    {
        Matrix4x4 m = GUI.matrix;
        Color oc = GUI.color;
        GUI.color = c;

        Vector2 d = b - a;
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        float len = d.magnitude;
        GUIUtility.RotateAroundPivot(angle, a);
        GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, len, thickness), Tex1x1());
        GUI.matrix = m;
        GUI.color = oc;
    }

    void DrawRect(Rect r, Color c, float thickness)
    {
        DrawLine(new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), c, thickness);
        DrawLine(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), c, thickness);
        DrawLine(new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), c, thickness);
        DrawLine(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), c, thickness);
    }

    void DrawFilledRect(Rect r, Color c)
    {
        Color oc = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Tex1x1());
        GUI.color = oc;
    }
}
