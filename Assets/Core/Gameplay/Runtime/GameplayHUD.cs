using UnityEngine;

public class GameplayHUD : MonoBehaviour
{
    public GameManager gm;

    [Header("Style")]
    public int timerFontSize = 48;
    public int infoFontSize = 20;
    public int countdownFontSize = 120;
    public Color normalColor = Color.white;
    public Color warningColor = new Color(1f, 0.3f, 0.2f);
    public float warningThreshold = 10f;

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
        if (gm == null) return;
        if (Event.current.type != EventType.Repaint) return;

        switch (gm.CurrentState)
        {
            case GameManager.State.Countdown:
                DrawCountdown();
                break;
            case GameManager.State.Playing:
                DrawPlayingHUD();
                break;
            case GameManager.State.TimeUp:
                DrawTimeUp();
                break;
        }
    }

    void DrawCountdown()
    {
        int val = Mathf.CeilToInt(gm.CountdownValue);
        string text = val > 0 ? val.ToString() : "GO!";

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = countdownFontSize,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = Color.white;

        // 半透明背景
        Color oc = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.4f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Tex1x1());
        GUI.color = oc;

        Rect r = new Rect(0, Screen.height * 0.3f, Screen.width, countdownFontSize + 20f);
        GUI.Label(r, text, style);

        // 關卡資訊
        if (gm.levelConfig != null)
        {
            GUIStyle info = new GUIStyle(GUI.skin.label)
            {
                fontSize = infoFontSize,
                alignment = TextAnchor.MiddleCenter
            };
            info.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
            float infoY = Screen.height * 0.55f;
            GUI.Label(new Rect(0, infoY, Screen.width, 30), gm.levelConfig.levelName, info);
            string goal = gm.levelConfig.requiredCount + " photos >= " + Mathf.RoundToInt(gm.levelConfig.requiredScore) + " pts";
            GUI.Label(new Rect(0, infoY + 30, Screen.width, 30), goal, info);
        }
    }

    void DrawPlayingHUD()
    {
        float time = gm.RemainingTime;
        bool warn = time <= warningThreshold;

        // 剩餘時間（右上角）
        GUIStyle timerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = timerFontSize,
            alignment = TextAnchor.UpperRight,
            fontStyle = FontStyle.Bold
        };
        timerStyle.normal.textColor = warn ? FlashColor(warningColor) : normalColor;

        int min = (int)(time / 60f);
        int sec = (int)(time % 60f);
        string timeStr = min > 0 ? min + ":" + sec.ToString("00") : sec.ToString();
        GUI.Label(new Rect(Screen.width - 220, 10, 200, timerFontSize + 10), timeStr, timerStyle);

        // 照片統計（右上角，時間下方）
        GUIStyle infoStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = infoFontSize,
            alignment = TextAnchor.UpperRight
        };
        infoStyle.normal.textColor = normalColor;

        float y = timerFontSize + 20;
        int validCount = gm.GetValidCount();
        int totalCount = gm.Photos.Count;
        float bestScore = gm.GetBestScore();

        if (gm.levelConfig != null)
        {
            string progress = validCount + " / " + gm.levelConfig.requiredCount + " valid";
            Color progColor = validCount >= gm.levelConfig.requiredCount
                ? new Color(0.2f, 1f, 0.3f)
                : normalColor;
            infoStyle.normal.textColor = progColor;
            GUI.Label(new Rect(Screen.width - 220, y, 200, 26), progress, infoStyle);
            y += 26;
        }

        infoStyle.normal.textColor = normalColor;
        GUI.Label(new Rect(Screen.width - 220, y, 200, 26), "Photos: " + totalCount, infoStyle);
        y += 26;

        if (bestScore > 0f)
            GUI.Label(new Rect(Screen.width - 220, y, 200, 26), "Best: " + Mathf.RoundToInt(bestScore) + " pts", infoStyle);
    }

    void DrawTimeUp()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 80,
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        style.normal.textColor = warningColor;
        GUI.Label(new Rect(0, Screen.height * 0.35f, Screen.width, 100), "TIME UP", style);
    }

    Color FlashColor(Color c)
    {
        float a = Mathf.Abs(Mathf.Sin(Time.time * 4f));
        return new Color(c.r, c.g, c.b, 0.5f + a * 0.5f);
    }
}
