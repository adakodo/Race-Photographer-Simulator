using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using FluffyUnderware.Curvy;

/// <summary>
/// TrackWidthProfile 自訂編輯器。
/// 提供射線自動偵測賽道寬度、Scene View 走廊視覺化、以及可拖曳的寬度控制點。
/// </summary>
[CustomEditor(typeof(TrackWidthProfile))]
public class TrackWidthProfileEditor : Editor
{
    // ── 序列化屬性 ──
    private SerializedProperty keysProp;

    // ── 射線偵測設定 ──
    private float sampleInterval = 5f;
    private LayerMask raycastLayerMask = ~0;
    private float maxRaycastDistance = 30f;
    private float fallbackWidth = 2f;
    private float raycastHeightOffset = 0.5f;

    // ── 場景視覺化設定 ──
    private int visualizationSamples = 100;
    private bool showCorridor = true;
    private bool addKeyMode = false;

    // ── Foldout 狀態 ──
    private bool raycastFoldout = true;
    private bool visFoldout = true;

    // ── EditorPrefs 鍵名前綴 ──
    private const string PREFS_PREFIX = "TrackWidthEditor_";

    private void OnEnable()
    {
        keysProp = serializedObject.FindProperty("keys");
        LoadPrefs();
    }

    private void OnDisable()
    {
        SavePrefs();
    }

    // ====================================================================
    // Inspector GUI
    // ====================================================================

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ── Keys 列表 ──
        EditorGUILayout.PropertyField(keysProp, new GUIContent("寬度關鍵點"), true);

        EditorGUILayout.Space(10);

        // ── 射線偵測設定 ──
        raycastFoldout = EditorGUILayout.Foldout(raycastFoldout, "射線偵測設定", true, EditorStyles.foldoutHeader);
        if (raycastFoldout)
        {
            EditorGUI.indentLevel++;
            sampleInterval = EditorGUILayout.FloatField("取樣間距 (公尺)", sampleInterval);
            sampleInterval = Mathf.Max(0.5f, sampleInterval);

            raycastLayerMask = EditorGUILayout.MaskField("偵測 Layer",
                InternalEditorUtility.LayerMaskToConcatenatedLayersMask(raycastLayerMask),
                InternalEditorUtility.layers);
            raycastLayerMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(raycastLayerMask);

            maxRaycastDistance = EditorGUILayout.FloatField("最大射線距離", maxRaycastDistance);
            maxRaycastDistance = Mathf.Max(1f, maxRaycastDistance);

            fallbackWidth = EditorGUILayout.FloatField("預設寬度 (未命中)", fallbackWidth);
            fallbackWidth = Mathf.Max(0.1f, fallbackWidth);

            raycastHeightOffset = EditorGUILayout.FloatField("射線高度偏移", raycastHeightOffset);

            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("執行射線偵測", GUILayout.Height(28)))
                {
                    PerformRaycastDetection();
                }
                if (GUILayout.Button("清除所有 Keys", GUILayout.Width(110), GUILayout.Height(28)))
                {
                    if (EditorUtility.DisplayDialog("確認", "確定要清除所有寬度關鍵點嗎？", "確定", "取消"))
                    {
                        ClearAllKeys();
                    }
                }
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(6);

        // ── 場景視覺化設定 ──
        visFoldout = EditorGUILayout.Foldout(visFoldout, "場景視覺化設定", true, EditorStyles.foldoutHeader);
        if (visFoldout)
        {
            EditorGUI.indentLevel++;
            showCorridor = EditorGUILayout.Toggle("顯示走廊", showCorridor);
            visualizationSamples = EditorGUILayout.IntSlider("走廊取樣數", visualizationSamples, 20, 500);

            EditorGUILayout.Space(2);
            Color prevBg = GUI.backgroundColor;
            if (addKeyMode)
                GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
            addKeyMode = GUILayout.Toggle(addKeyMode, addKeyMode ? "● 新增模式 ON (Shift+Click)" : "○ 新增模式 OFF",
                "Button", GUILayout.Height(24));
            GUI.backgroundColor = prevBg;

            if (addKeyMode)
            {
                EditorGUILayout.HelpBox("在 Scene View 中按住 Shift + 左鍵點擊賽道附近，即可新增寬度關鍵點。", MessageType.Info);
            }

            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    // ====================================================================
    // 射線偵測
    // ====================================================================

    /// <summary>
    /// 沿 Spline 等距取樣，向左右發射射線偵測賽道邊界寬度，自動填入 keys。
    /// </summary>
    private void PerformRaycastDetection()
    {
        TrackSplineProvider provider = FindProvider();
        if (provider == null) return;
        if (!EnsureSplineReady(provider)) return;

        float length = provider.Length;
        if (length <= 0f)
        {
            EditorUtility.DisplayDialog("錯誤", "Spline 長度為 0，無法偵測。", "確定");
            return;
        }

        int sampleCount = Mathf.Max(2, Mathf.CeilToInt(length / sampleInterval));

        // 記錄 Undo
        Undo.RecordObject(target, "自動偵測賽道寬度");
        serializedObject.Update();

        // 清空現有 keys
        keysProp.ClearArray();

        int hitCount = 0;
        int totalRays = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            float distance = length * i / sampleCount;
            TrackSplineProvider.TrackSample sample = provider.GetSampleByDistance(distance);
            float tf = sample.tf;

            // 射線起點抬高，避免打到地面
            Vector3 origin = sample.position + sample.up * raycastHeightOffset;

            // 向左偵測（-right 方向）
            float leftWidth = fallbackWidth;
            totalRays++;
            if (Physics.Raycast(origin, -sample.right, out RaycastHit hitLeft,
                                maxRaycastDistance, raycastLayerMask, QueryTriggerInteraction.Ignore))
            {
                leftWidth = hitLeft.distance;
                hitCount++;
            }

            // 向右偵測（+right 方向）
            float rightWidth = fallbackWidth;
            totalRays++;
            if (Physics.Raycast(origin, sample.right, out RaycastHit hitRight,
                                maxRaycastDistance, raycastLayerMask, QueryTriggerInteraction.Ignore))
            {
                rightWidth = hitRight.distance;
                hitCount++;
            }

            // 寫入新 key
            keysProp.InsertArrayElementAtIndex(keysProp.arraySize);
            SerializedProperty newKey = keysProp.GetArrayElementAtIndex(keysProp.arraySize - 1);
            newKey.FindPropertyRelative("tf").floatValue = tf;
            newKey.FindPropertyRelative("leftWidth").floatValue = leftWidth;
            newKey.FindPropertyRelative("rightWidth").floatValue = rightWidth;
        }

        serializedObject.ApplyModifiedProperties();

        // 結果提示
        string msg = $"偵測完成！\n取樣點數：{sampleCount}\n射線命中：{hitCount} / {totalRays}";
        if (hitCount == 0)
            msg += "\n\n⚠ 沒有任何射線命中，請確認 LayerMask 設定與場景中的 Collider。";
        EditorUtility.DisplayDialog("射線偵測結果", msg, "確定");

        SceneView.RepaintAll();
    }

    /// <summary>
    /// 清除所有寬度關鍵點。
    /// </summary>
    private void ClearAllKeys()
    {
        Undo.RecordObject(target, "清除所有寬度關鍵點");
        serializedObject.Update();
        keysProp.ClearArray();
        serializedObject.ApplyModifiedProperties();
        SceneView.RepaintAll();
    }

    // ====================================================================
    // Scene View
    // ====================================================================

    private void OnSceneGUI()
    {
        TrackSplineProvider provider = FindProvider();
        if (provider == null || !EnsureSplineReady(provider)) return;

        serializedObject.Update();

        if (showCorridor)
            DrawWidthCorridor(provider);

        DrawKeyHandles(provider);

        if (addKeyMode)
            HandleNewKeyCreation(provider);

        if (serializedObject.ApplyModifiedProperties())
            SceneView.RepaintAll();
    }

    /// <summary>
    /// 繪製賽道寬度走廊視覺化（橘色左邊界、藍色右邊界、白色中心線）。
    /// </summary>
    private void DrawWidthCorridor(TrackSplineProvider provider)
    {
        TrackWidthProfile profile = (TrackWidthProfile)target;
        float length = provider.Length;
        if (length <= 0f) return;

        int steps = visualizationSamples;
        Vector3 prevLeft = Vector3.zero;
        Vector3 prevRight = Vector3.zero;
        Vector3 prevCenter = Vector3.zero;

        // 計算橫線間隔（每 5% 畫一條）
        int crossLineInterval = Mathf.Max(1, steps / 20);

        for (int i = 0; i <= steps; i++)
        {
            float dist = length * i / steps;
            TrackSplineProvider.TrackSample sample = provider.GetSampleByDistance(dist);
            float leftW = profile.GetLeftWidth(sample.tf);
            float rightW = profile.GetRightWidth(sample.tf);

            Vector3 leftPt = sample.position - sample.right * leftW;
            Vector3 rightPt = sample.position + sample.right * rightW;

            if (i > 0)
            {
                // 左邊界（橘色）
                Handles.color = new Color(1f, 0.4f, 0f, 0.8f);
                Handles.DrawLine(prevLeft, leftPt);

                // 右邊界（藍色）
                Handles.color = new Color(0f, 0.6f, 1f, 0.8f);
                Handles.DrawLine(prevRight, rightPt);

                // 中心線（淡白色）
                Handles.color = new Color(1f, 1f, 1f, 0.15f);
                Handles.DrawLine(prevCenter, sample.position);
            }

            // 每隔一段畫黃色橫線連接左右
            if (i % crossLineInterval == 0)
            {
                Handles.color = new Color(1f, 1f, 0f, 0.25f);
                Handles.DrawLine(leftPt, rightPt);
            }

            prevLeft = leftPt;
            prevRight = rightPt;
            prevCenter = sample.position;
        }
    }

    /// <summary>
    /// 繪製每個 Key 的可拖曳控制點（左橘/右藍球形 Handle）。
    /// </summary>
    private void DrawKeyHandles(TrackSplineProvider provider)
    {
        int count = keysProp.arraySize;
        for (int i = 0; i < count; i++)
        {
            SerializedProperty keyProp = keysProp.GetArrayElementAtIndex(i);
            SerializedProperty tfProp = keyProp.FindPropertyRelative("tf");
            SerializedProperty leftProp = keyProp.FindPropertyRelative("leftWidth");
            SerializedProperty rightProp = keyProp.FindPropertyRelative("rightWidth");

            float tf = tfProp.floatValue;
            float dist = provider.TFToDistance(tf);
            TrackSplineProvider.TrackSample sample = provider.GetSampleByDistance(dist);

            float leftW = leftProp.floatValue;
            float rightW = rightProp.floatValue;

            Vector3 leftPos = sample.position - sample.right * leftW;
            Vector3 rightPos = sample.position + sample.right * rightW;

            float handleSize = HandleUtility.GetHandleSize(sample.position) * 0.08f;

            // ── 左側拖曳 Handle（橘色）──
            Handles.color = new Color(1f, 0.4f, 0f);
            EditorGUI.BeginChangeCheck();
            Vector3 newLeft = Handles.Slider(leftPos, -sample.right, handleSize * 3f,
                Handles.SphereHandleCap, 0.01f);
            if (EditorGUI.EndChangeCheck())
            {
                // 計算新寬度：投影到 right 軸上的距離
                float newWidth = Vector3.Dot(sample.position - newLeft, sample.right);
                leftProp.floatValue = Mathf.Max(0f, newWidth);
            }

            // ── 右側拖曳 Handle（藍色）──
            Handles.color = new Color(0f, 0.6f, 1f);
            EditorGUI.BeginChangeCheck();
            Vector3 newRight = Handles.Slider(rightPos, sample.right, handleSize * 3f,
                Handles.SphereHandleCap, 0.01f);
            if (EditorGUI.EndChangeCheck())
            {
                float newWidth = Vector3.Dot(newRight - sample.position, sample.right);
                rightProp.floatValue = Mathf.Max(0f, newWidth);
            }

            // ── 黃色橫線連接左右 ──
            Handles.color = new Color(1f, 1f, 0f, 0.6f);
            Handles.DrawLine(leftPos, rightPos);

            // ── 中心標記 ──
            Handles.color = new Color(1f, 1f, 1f, 0.5f);
            Handles.DrawWireCube(sample.position, Vector3.one * handleSize * 1.5f);

            // ── 標籤 ──
            Handles.color = Color.white;
            string label = $"[{i}] tf={tf:F3}\nL={leftW:F2}  R={rightW:F2}";
            Handles.Label(sample.position + sample.up * 1.2f, label, EditorStyles.whiteBoldLabel);
        }
    }

    /// <summary>
    /// 新增模式下，Shift+左鍵點擊 Scene View 新增寬度關鍵點。
    /// </summary>
    private void HandleNewKeyCreation(TrackSplineProvider provider)
    {
        Event e = Event.current;

        // 顯示提示游標
        if (e.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        if (e.type == EventType.MouseDown && e.shift && e.button == 0)
        {
            // 從滑鼠位置發射射線，嘗試打到場景中的物件
            Ray mouseRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 worldPoint;

            if (Physics.Raycast(mouseRay, out RaycastHit hit, 1000f))
            {
                worldPoint = hit.point;
            }
            else
            {
                // 若未命中任何物件，使用射線與 Y=0 平面的交點
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                if (groundPlane.Raycast(mouseRay, out float enter))
                {
                    worldPoint = mouseRay.GetPoint(enter);
                }
                else
                {
                    return; // 無法確定世界座標，放棄
                }
            }

            // 投影到 spline 上找最近點
            float nearestDist = provider.GetNearestDistance(worldPoint);
            float tf = provider.DistanceToTF(nearestDist);

            // 取得該位置目前的寬度作為預設值
            TrackWidthProfile profile = (TrackWidthProfile)target;
            float defaultLeft = profile.GetLeftWidth(tf);
            float defaultRight = profile.GetRightWidth(tf);

            Undo.RecordObject(target, "新增寬度關鍵點");
            serializedObject.Update();

            int insertIdx = keysProp.arraySize;
            keysProp.InsertArrayElementAtIndex(insertIdx);
            SerializedProperty newKey = keysProp.GetArrayElementAtIndex(insertIdx);
            newKey.FindPropertyRelative("tf").floatValue = tf;
            newKey.FindPropertyRelative("leftWidth").floatValue = defaultLeft;
            newKey.FindPropertyRelative("rightWidth").floatValue = defaultRight;

            serializedObject.ApplyModifiedProperties();
            e.Use();
            Repaint();
        }
    }

    // ====================================================================
    // 輔助方法
    // ====================================================================

    /// <summary>
    /// 從同物件或父物件尋找 TrackSplineProvider。
    /// </summary>
    private TrackSplineProvider FindProvider()
    {
        var twp = (TrackWidthProfile)target;
        var provider = twp.GetComponent<TrackSplineProvider>();
        if (provider == null)
            provider = twp.GetComponentInParent<TrackSplineProvider>();
        return provider;
    }

    /// <summary>
    /// 確保 Curvy Spline 已初始化，未初始化則呼叫 Refresh。
    /// </summary>
    private bool EnsureSplineReady(TrackSplineProvider provider)
    {
        if (provider.Spline == null) return false;
        if (!provider.Spline.IsInitialized)
            provider.Spline.Refresh();
        return provider.IsValid();
    }

    // ── EditorPrefs 儲存/載入 ──

    private void SavePrefs()
    {
        EditorPrefs.SetFloat(PREFS_PREFIX + "sampleInterval", sampleInterval);
        EditorPrefs.SetInt(PREFS_PREFIX + "raycastLayerMask", raycastLayerMask);
        EditorPrefs.SetFloat(PREFS_PREFIX + "maxRaycastDistance", maxRaycastDistance);
        EditorPrefs.SetFloat(PREFS_PREFIX + "fallbackWidth", fallbackWidth);
        EditorPrefs.SetFloat(PREFS_PREFIX + "raycastHeightOffset", raycastHeightOffset);
        EditorPrefs.SetInt(PREFS_PREFIX + "visualizationSamples", visualizationSamples);
        EditorPrefs.SetBool(PREFS_PREFIX + "showCorridor", showCorridor);
    }

    private void LoadPrefs()
    {
        sampleInterval = EditorPrefs.GetFloat(PREFS_PREFIX + "sampleInterval", 5f);
        raycastLayerMask = EditorPrefs.GetInt(PREFS_PREFIX + "raycastLayerMask", ~0);
        maxRaycastDistance = EditorPrefs.GetFloat(PREFS_PREFIX + "maxRaycastDistance", 30f);
        fallbackWidth = EditorPrefs.GetFloat(PREFS_PREFIX + "fallbackWidth", 2f);
        raycastHeightOffset = EditorPrefs.GetFloat(PREFS_PREFIX + "raycastHeightOffset", 0.5f);
        visualizationSamples = EditorPrefs.GetInt(PREFS_PREFIX + "visualizationSamples", 100);
        showCorridor = EditorPrefs.GetBool(PREFS_PREFIX + "showCorridor", true);
    }
}
