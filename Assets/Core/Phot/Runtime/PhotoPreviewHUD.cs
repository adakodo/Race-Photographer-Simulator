using UnityEngine;

public class PhotoPreviewHUD : MonoBehaviour
{
    public PhotoCaptureManager capture;
    public PhotoScoreEvaluator scorer;

    [Header("顯示設定")]
    public float displayDuration = 3f;
    public float fadeOutDuration = 0.5f;
    public float thumbWidth = 200f;
    public float margin = 16f;
    public float scoreBarHeight = 24f;

    Texture2D currentThumb;
    PhotoScoreEvaluator.PhotoScore currentScore;
    float showTime;
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

    void OnEnable()
    {
        if (capture != null) capture.OnPhotoReady += OnPhoto;
    }

    void OnDisable()
    {
        if (capture != null) capture.OnPhotoReady -= OnPhoto;
    }

    void OnPhoto(Texture2D tex)
    {
        // 複製一份縮圖
        if (currentThumb == null || currentThumb.width != tex.width || currentThumb.height != tex.height)
            currentThumb = new Texture2D(tex.width, tex.height, tex.format, false, false);

        Graphics.CopyTexture(tex, currentThumb);
        currentThumb.Apply(false, false);

        currentScore = scorer != null ? scorer.Evaluate() : null;
        showTime = Time.unscaledTime;
    }

    void OnGUI()
    {
        if (currentThumb == null) return;
        if (Event.current.type != EventType.Repaint) return;

        float elapsed = Time.unscaledTime - showTime;
        float totalTime = displayDuration + fadeOutDuration;
        if (elapsed > totalTime) return;

        // 計算透明度
        float alpha = 1f;
        if (elapsed > displayDuration)
            alpha = 1f - (elapsed - displayDuration) / fadeOutDuration;

        DrawPreview(alpha);
    }

    void DrawPreview(float alpha)
    {
        float aspect = (float)currentThumb.height / currentThumb.width;
        float thumbH = thumbWidth * aspect;
        float totalH = thumbH + scoreBarHeight;

        // 左下角定位（底部 HUD bar 上方）
        float x = margin;
        float y = Screen.height - totalH - margin - 50f;

        Color oc = GUI.color;

        // 背景
        GUI.color = new Color(0f, 0f, 0f, 0.7f * alpha);
        GUI.DrawTexture(new Rect(x - 4, y - 4, thumbWidth + 8, totalH + 8), Tex1x1());
        GUI.color = oc;

        // 縮圖
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTexture(new Rect(x, y, thumbWidth, thumbH), currentThumb, ScaleMode.ScaleToFit);
        GUI.color = oc;

        // 分數
        if (currentScore != null)
        {
            GUIStyle scoreStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            scoreStyle.normal.textColor = ScoreColor(currentScore.total, alpha);

            Rect scoreRect = new Rect(x, y + thumbH, thumbWidth, scoreBarHeight);
            GUI.Label(scoreRect, Mathf.RoundToInt(currentScore.total) + " pts", scoreStyle);
        }
    }

    Color ScoreColor(float score, float alpha)
    {
        if (score >= 80f) return new Color(0.2f, 1f, 0.3f, alpha);
        if (score >= 60f) return new Color(1f, 0.9f, 0.2f, alpha);
        return new Color(1f, 0.4f, 0.3f, alpha);
    }
}
