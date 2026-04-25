using UnityEngine;

public class SettlementUI : MonoBehaviour
{
    public GameManager gm;

    [Header("Style")]
    public int titleFontSize = 40;
    public int bodyFontSize = 20;
    public int buttonFontSize = 24;
    public float thumbSize = 120f;
    public int thumbsPerRow = 6;
    public Color bgColor = new Color(0.05f, 0.05f, 0.1f, 0.95f);

    Texture2D _tex1x1;
    Vector2 scrollPos;

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
        if (gm == null || gm.CurrentState != GameManager.State.Settlement) return;

        Rect screen = new Rect(0, 0, Screen.width, Screen.height);

        // 背景
        Color oc = GUI.color;
        GUI.color = bgColor;
        GUI.DrawTexture(screen, Tex1x1());
        GUI.color = oc;

        float cx = Screen.width * 0.5f;
        float y = 30f;

        // 標題
        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = titleFontSize,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(0, y, Screen.width, titleFontSize + 10), "RESULTS", titleStyle);
        y += titleFontSize + 20;

        // 過關/失敗 + 星級
        bool passed = gm.IsPassed();
        int stars = gm.GetStars();

        GUIStyle resultStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };

        if (passed)
        {
            string starStr = new string('★', stars) + new string('☆', 3 - stars);
            resultStyle.normal.textColor = StarColor(stars);
            GUI.Label(new Rect(0, y, Screen.width, 40), starStr + "  " + StarText(stars), resultStyle);
        }
        else
        {
            resultStyle.normal.textColor = new Color(1f, 0.3f, 0.2f);
            GUI.Label(new Rect(0, y, Screen.width, 40), "FAILED", resultStyle);
        }
        y += 50;

        // 目標
        if (gm.levelConfig != null)
        {
            GUIStyle goalStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = bodyFontSize,
                alignment = TextAnchor.MiddleCenter
            };
            int validCount = gm.GetValidCount();
            string checkMark = passed ? " ✓" : " ✗";
            goalStyle.normal.textColor = passed ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.4f, 0.3f);
            string goalText = "Goal: " + gm.levelConfig.requiredCount + " photos >= "
                + Mathf.RoundToInt(gm.levelConfig.requiredScore) + " pts  ("
                + validCount + " achieved)" + checkMark;
            GUI.Label(new Rect(0, y, Screen.width, 28), goalText, goalStyle);
            y += 36;
        }

        // 統計
        GUIStyle statStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = bodyFontSize,
            alignment = TextAnchor.MiddleCenter
        };
        statStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);

        string stats = "Photos: " + gm.Photos.Count
            + "    Best: " + Mathf.RoundToInt(gm.GetBestScore()) + " pts"
            + "    Average: " + Mathf.RoundToInt(gm.GetAverageScore()) + " pts";
        GUI.Label(new Rect(0, y, Screen.width, 28), stats, statStyle);
        y += 40;

        // 照片縮圖
        DrawPhotoGrid(y);

        // 按鈕
        DrawButtons();
    }

    void DrawPhotoGrid(float startY)
    {
        var photos = gm.Photos;
        if (photos.Count == 0) return;

        float pad = 6f;
        float totalW = thumbsPerRow * (thumbSize + pad) + pad;
        float startX = (Screen.width - totalW) * 0.5f;
        float thumbH = thumbSize * 0.67f;
        float cellH = thumbH + 24f + pad;

        float areaH = Screen.height - startY - 80f;
        int rows = Mathf.CeilToInt((float)photos.Count / thumbsPerRow);
        float contentH = rows * cellH + pad;

        Rect viewRect = new Rect(startX - pad, startY, totalW + pad * 2, areaH);
        Rect contentRect = new Rect(0, 0, totalW, contentH);

        scrollPos = GUI.BeginScrollView(viewRect, scrollPos, contentRect);

        GUIStyle scoreStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };

        for (int i = 0; i < photos.Count; i++)
        {
            int col = i % thumbsPerRow;
            int row = i / thumbsPerRow;
            float x = pad + col * (thumbSize + pad);
            float y = pad + row * cellH;

            Rect thumbRect = new Rect(x, y, thumbSize, thumbH);

            if (photos[i].texture != null)
                GUI.DrawTexture(thumbRect, photos[i].texture, ScaleMode.ScaleToFit);

            if (photos[i].score != null)
            {
                float score = photos[i].score.total;
                bool valid = gm.levelConfig != null && score >= gm.levelConfig.requiredScore;
                scoreStyle.normal.textColor = valid
                    ? new Color(0.2f, 1f, 0.3f)
                    : new Color(0.7f, 0.7f, 0.7f);
                GUI.Label(new Rect(x, y + thumbH, thumbSize, 20),
                    Mathf.RoundToInt(score) + " pts", scoreStyle);
            }
        }

        GUI.EndScrollView();
    }

    void DrawButtons()
    {
        float btnW = 160f;
        float btnH = 50f;
        float y = Screen.height - btnH - 20f;
        float cx = Screen.width * 0.5f;

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = buttonFontSize
        };

        if (GUI.Button(new Rect(cx - btnW * 0.5f, y, btnW, btnH), "Retry", btnStyle))
        {
            gm.RetryLevel();
        }
    }

    Color StarColor(int stars)
    {
        if (stars >= 3) return new Color(1f, 0.85f, 0f);
        if (stars >= 2) return new Color(0.7f, 0.85f, 1f);
        return new Color(0.6f, 0.8f, 0.6f);
    }

    string StarText(int stars)
    {
        if (stars >= 3) return "EXCELLENT!";
        if (stars >= 2) return "GREAT!";
        return "CLEAR";
    }
}
