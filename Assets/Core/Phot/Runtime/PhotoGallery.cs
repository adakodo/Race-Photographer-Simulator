using System.Collections.Generic;
using UnityEngine;

public class PhotoGallery : MonoBehaviour
{
    public PhotoCaptureManager capture;
    public PhotoScoreEvaluator scorer;

    [Header("Keys")]
    public KeyCode toggleKey = KeyCode.F1;
    public KeyCode deleteKey = KeyCode.Delete;

    [Header("Layout")]
    public int thumbsPerRow = 5;
    public float thumbSize = 160f;
    public float padding = 8f;
    public Color bgColor = new Color(0f, 0f, 0f, 0.85f);
    public Color selectedColor = new Color(1f, 0.8f, 0f, 0.8f);

    public bool IsOpen { get; private set; }

    [System.Serializable]
    public class PhotoEntry
    {
        public Texture2D texture;
        public PhotoScoreEvaluator.PhotoScore score;
    }

    List<PhotoEntry> photos = new List<PhotoEntry>();
    int selectedIndex = -1;
    bool fullScreen;
    Vector2 scrollPos;
    Texture2D _tex1x1;
    float prevTimeScale;

    void OnEnable()
    {
        if (capture != null) capture.OnPhotoReady += OnNewPhoto;
    }

    void OnDisable()
    {
        if (capture != null) capture.OnPhotoReady -= OnNewPhoto;
    }

    void OnNewPhoto(Texture2D tex)
    {
        var copy = new Texture2D(tex.width, tex.height, tex.format, false, false);
        Graphics.CopyTexture(tex, copy);
        copy.Apply(false, false);

        var entry = new PhotoEntry { texture = copy };
        if (scorer != null) entry.score = scorer.Evaluate();
        photos.Add(entry);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) ToggleGallery();

        if (!IsOpen) return;

        if (fullScreen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) fullScreen = false;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) NavigatePhoto(-1);
            if (Input.GetKeyDown(KeyCode.RightArrow)) NavigatePhoto(1);
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CloseGallery();
        }

        if (Input.GetKeyDown(deleteKey) && selectedIndex >= 0 && selectedIndex < photos.Count)
        {
            if (photos[selectedIndex].texture != null) Destroy(photos[selectedIndex].texture);
            photos.RemoveAt(selectedIndex);
            if (selectedIndex >= photos.Count) selectedIndex = photos.Count - 1;
            if (photos.Count == 0) fullScreen = false;
        }
    }

    void ToggleGallery()
    {
        if (IsOpen) CloseGallery();
        else OpenGallery();
    }

    void OpenGallery()
    {
        IsOpen = true;
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void CloseGallery()
    {
        IsOpen = false;
        fullScreen = false;
        Time.timeScale = prevTimeScale;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void NavigatePhoto(int dir)
    {
        if (photos.Count == 0) return;
        selectedIndex = Mathf.Clamp(selectedIndex + dir, 0, photos.Count - 1);
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
        if (!IsOpen || Event.current.type != EventType.Repaint && Event.current.type != EventType.MouseDown) return;

        if (fullScreen && selectedIndex >= 0 && selectedIndex < photos.Count)
            DrawFullScreen();
        else
            DrawThumbnails();
    }

    void DrawThumbnails()
    {
        Rect screen = new Rect(0, 0, Screen.width, Screen.height);

        // 背景
        Color oc = GUI.color;
        GUI.color = bgColor;
        GUI.DrawTexture(screen, Tex1x1());
        GUI.color = oc;

        // 標題
        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            alignment = TextAnchor.MiddleCenter
        };
        title.normal.textColor = Color.white;
        GUI.Label(new Rect(0, 10, Screen.width, 40), "GALLERY (" + photos.Count + ")", title);

        if (photos.Count == 0)
        {
            GUIStyle empty = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter
            };
            empty.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
            GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 40), "-- No Photos --", empty);
            return;
        }

        float startY = 60f;
        float totalW = thumbsPerRow * (thumbSize + padding) + padding;
        float startX = (Screen.width - totalW) * 0.5f;

        for (int i = 0; i < photos.Count; i++)
        {
            int col = i % thumbsPerRow;
            int row = i / thumbsPerRow;
            float x = startX + padding + col * (thumbSize + padding);
            float y = startY + row * (thumbSize * 0.67f + padding + 20f);

            Rect thumbRect = new Rect(x, y, thumbSize, thumbSize * 0.67f);

            // 選中框
            if (i == selectedIndex)
            {
                GUI.color = selectedColor;
                GUI.DrawTexture(new Rect(thumbRect.x - 3, thumbRect.y - 3, thumbRect.width + 6, thumbRect.height + 6), Tex1x1());
                GUI.color = oc;
            }

            // 縮圖
            if (photos[i].texture != null)
                GUI.DrawTexture(thumbRect, photos[i].texture, ScaleMode.ScaleToFit);

            // 點擊選取
            if (Event.current.type == EventType.MouseDown && thumbRect.Contains(Event.current.mousePosition))
            {
                if (selectedIndex == i)
                    fullScreen = true;
                else
                    selectedIndex = i;
                Event.current.Use();
            }

            // 分數
            if (photos[i].score != null)
            {
                GUIStyle scoreStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
                scoreStyle.normal.textColor = ScoreColor(photos[i].score.total);
                GUI.Label(new Rect(x, y + thumbSize * 0.67f, thumbSize, 18f),
                    Mathf.RoundToInt(photos[i].score.total) + " pts", scoreStyle);
            }
        }
    }

    void DrawFullScreen()
    {
        var entry = photos[selectedIndex];
        Rect screen = new Rect(0, 0, Screen.width, Screen.height);

        Color oc = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(screen, Tex1x1());
        GUI.color = oc;

        if (entry.texture != null)
            GUI.DrawTexture(screen, entry.texture, ScaleMode.ScaleToFit);

        // EXIF 資訊
        if (entry.score != null)
        {
            GUIStyle info = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            info.normal.textColor = new Color(1f, 1f, 1f, 0.85f);

            float y = Screen.height - 140f;
            float x = 20f;
            float lh = 22f;

            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0, y - 10f, 320f, 150f), Tex1x1());
            GUI.color = oc;

            var s = entry.score;
            GUI.Label(new Rect(x, y, 300, lh), FormatShutter(s.shutterSpeed) + "  f/" + s.aperture.ToString("0.0") + "  ISO " + s.iso, info);
            GUI.Label(new Rect(x, y + lh, 300, lh), Mathf.RoundToInt(s.focalLength) + "mm  Focus: " + s.focusDistance.ToString("0.0") + "m", info);
            GUI.Label(new Rect(x, y + lh * 2, 300, lh), s.timestamp, info);

            // 分數明細
            GUIStyle scoreInfo = new GUIStyle(info);
            scoreInfo.normal.textColor = ScoreColor(s.total);
            GUI.Label(new Rect(x, y + lh * 3, 300, lh),
                "Score: " + Mathf.RoundToInt(s.total) + "  (C:" + Mathf.RoundToInt(s.composition)
                + " E:" + Mathf.RoundToInt(s.exposure) + " S:" + Mathf.RoundToInt(s.sharpness)
                + " F:" + Mathf.RoundToInt(s.focus) + ")", scoreInfo);

            // 導覽提示
            GUIStyle hint = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            hint.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
            GUI.Label(new Rect(0, 10, Screen.width, 30), (selectedIndex + 1) + " / " + photos.Count + "  [<-] [->] Navigate  [Esc] Back  [Del] Delete", hint);
        }
    }

    Color ScoreColor(float score)
    {
        if (score >= 80f) return new Color(0.2f, 1f, 0.3f);
        if (score >= 60f) return new Color(1f, 0.9f, 0.2f);
        return new Color(1f, 0.4f, 0.3f);
    }

    string FormatShutter(float sec)
    {
        if (sec < 1f)
        {
            int denom = Mathf.Max(1, Mathf.RoundToInt(1f / Mathf.Max(sec, 1e-6f)));
            return "1/" + denom;
        }
        return sec.ToString("0.0") + "s";
    }
}
