using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor 工具：沿 TrackSplineProvider 路徑自動將 Terrain 高度貼合賽道，
/// 邊緣平滑過渡到原始地形，並以高斯模糊消除接縫。
/// </summary>
public class TerrainTrackConformer : EditorWindow
{
    TrackSplineProvider splineProvider;
    Terrain terrain;

    float sampleInterval = 0.5f;
    float extraWidth = 2f;
    float falloffWidth = 6f;
    float heightOffset = -0.03f;
    int smoothIterations = 3;
    int smoothRadius = 3;

    [MenuItem("Tools/Terrain Track Conformer")]
    static void Open()
    {
        GetWindow<TerrainTrackConformer>("Track → Terrain");
    }

    void OnGUI()
    {
        GUILayout.Label("Terrain Track Conformer", EditorStyles.boldLabel);
        GUILayout.Space(8);

        splineProvider = (TrackSplineProvider)EditorGUILayout.ObjectField(
            "Track Spline Provider", splineProvider, typeof(TrackSplineProvider), true);

        terrain = (Terrain)EditorGUILayout.ObjectField(
            "Terrain", terrain, typeof(Terrain), true);

        GUILayout.Space(8);
        GUILayout.Label("取樣參數", EditorStyles.boldLabel);

        sampleInterval = EditorGUILayout.Slider("取樣間隔（公尺）", sampleInterval, 0.1f, 5f);
        extraWidth = EditorGUILayout.Slider("賽道外擴寬度（公尺）", extraWidth, 0f, 10f);
        falloffWidth = EditorGUILayout.Slider("邊緣過渡寬度（公尺）", falloffWidth, 1f, 20f);
        heightOffset = EditorGUILayout.Slider("高度偏移（公尺）", heightOffset, -0.5f, 0.5f);

        GUILayout.Space(8);
        GUILayout.Label("平滑設定", EditorStyles.boldLabel);

        smoothIterations = EditorGUILayout.IntSlider("平滑迭代次數", smoothIterations, 0, 10);
        smoothRadius = EditorGUILayout.IntSlider("平滑半徑（格）", smoothRadius, 1, 8);

        GUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "外擴寬度：賽道邊緣外再多壓平多少公尺\n" +
            "過渡寬度：從壓平區到原始地形的漸變距離（越大越平緩）\n" +
            "高度偏移：賽道下方偏移（負值=往下），避免 Z-fighting\n" +
            "平滑迭代：對修改區域做高斯模糊的次數（消除接縫）\n" +
            "平滑半徑：每次模糊的範圍（格數）",
            MessageType.Info);

        GUILayout.Space(12);

        EditorGUI.BeginDisabledGroup(splineProvider == null || terrain == null);
        if (GUILayout.Button("執行：貼合 Terrain", GUILayout.Height(32)))
        {
            Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Terrain Track Conform");
            ConformTerrain();
        }
        EditorGUI.EndDisabledGroup();
    }

    void ConformTerrain()
    {
        if (!splineProvider.IsValid())
        {
            EditorUtility.DisplayDialog("錯誤", "Spline 未初始化，請先在場景中啟用 CurvySpline。", "OK");
            return;
        }

        TerrainData td = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        int res = td.heightmapResolution;
        float[,] heights = td.GetHeights(0, 0, res, res);
        float[,] original = (float[,])heights.Clone();

        float terrainWidth = td.size.x;
        float terrainLength = td.size.z;
        float terrainHeight = td.size.y;

        float splineLength = splineProvider.Length;
        int sampleCount = Mathf.CeilToInt(splineLength / Mathf.Max(0.1f, sampleInterval));

        // 每個 heightmap 點記錄：最近距離、目標高度、左右寬度
        float[,] minDist = new float[res, res];
        float[,] targetH = new float[res, res];
        float[,] trackW = new float[res, res];
        bool[,] affected = new bool[res, res];

        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                minDist[z, x] = float.MaxValue;

        var widthProfile = splineProvider.WidthProfile;

        // === 階段 1：沿 Spline 取樣 ===
        for (int i = 0; i <= sampleCount; i++)
        {
            float dist = (float)i / sampleCount * splineLength;
            var sample = splineProvider.GetSampleByDistance(dist);

            float leftW = (widthProfile != null) ? widthProfile.GetLeftWidth(sample.tf) : 2f;
            float rightW = (widthProfile != null) ? widthProfile.GetRightWidth(sample.tf) : 2f;
            float maxHalfW = Mathf.Max(leftW, rightW);
            float totalRange = maxHalfW + extraWidth + falloffWidth;

            // 影響範圍 bounding box
            int xStart = Mathf.Clamp(WorldToIdx(sample.position.x - totalRange, terrainPos.x, terrainWidth, res), 0, res - 1);
            int xEnd   = Mathf.Clamp(WorldToIdx(sample.position.x + totalRange, terrainPos.x, terrainWidth, res), 0, res - 1);
            int zStart = Mathf.Clamp(WorldToIdx(sample.position.z - totalRange, terrainPos.z, terrainLength, res), 0, res - 1);
            int zEnd   = Mathf.Clamp(WorldToIdx(sample.position.z + totalRange, terrainPos.z, terrainLength, res), 0, res - 1);

            for (int z = zStart; z <= zEnd; z++)
            {
                for (int x = xStart; x <= xEnd; x++)
                {
                    float wx = terrainPos.x + ((float)x / (res - 1)) * terrainWidth;
                    float wz = terrainPos.z + ((float)z / (res - 1)) * terrainLength;

                    Vector3 toPoint = new Vector3(wx - sample.position.x, 0, wz - sample.position.z);

                    // 有號橫向距離（正=右側，負=左側）
                    float signedLateral = Vector3.Dot(toPoint, sample.right);
                    float lateralDist = Mathf.Abs(signedLateral);
                    float longitudinalDist = Mathf.Abs(Vector3.Dot(toPoint, sample.forward));

                    if (longitudinalDist > sampleInterval * 2f) continue;

                    if (lateralDist < minDist[z, x])
                    {
                        minDist[z, x] = lateralDist;
                        // 考慮賽道傾斜（Banking）：用 right.y 計算橫向高度變化
                        float rightHorizSq = sample.right.x * sample.right.x
                                           + sample.right.z * sample.right.z;
                        float bankedY = sample.position.y;
                        if (rightHorizSq > 0.0001f)
                            bankedY += signedLateral * sample.right.y / rightHorizSq;
                        bankedY += heightOffset;
                        targetH[z, x] = (bankedY - terrainPos.y) / terrainHeight;
                        // 用左右各自的寬度
                        trackW[z, x] = (signedLateral >= 0) ? rightW : leftW;
                    }
                }
            }

            if (i % 100 == 0)
            {
                float progress = (float)i / sampleCount * 0.5f;
                if (EditorUtility.DisplayCancelableProgressBar("Terrain Track Conform",
                    $"取樣中 {i}/{sampleCount}", progress))
                {
                    EditorUtility.ClearProgressBar();
                    return;
                }
            }
        }

        // === 階段 2：套用高度 + Falloff ===
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                if (minDist[z, x] >= float.MaxValue) continue;

                float lateralDist = minDist[z, x];
                float innerEdge = trackW[z, x] + extraWidth;

                float t;
                if (lateralDist <= innerEdge)
                {
                    t = 1f;
                }
                else if (lateralDist <= innerEdge + falloffWidth)
                {
                    float f = (lateralDist - innerEdge) / falloffWidth;
                    // Quintic smootherstep（比 smoothstep 更平滑，二階導數連續）
                    t = 1f - f * f * f * (f * (f * 6f - 15f) + 10f);
                }
                else
                {
                    continue;
                }

                heights[z, x] = Mathf.Lerp(original[z, x], targetH[z, x], t);
                affected[z, x] = true;
            }
        }

        // === 階段 3：高斯模糊平滑（僅對受影響區域的邊緣） ===
        if (smoothIterations > 0)
        {
            for (int iter = 0; iter < smoothIterations; iter++)
            {
                float[,] smoothed = (float[,])heights.Clone();

                for (int z = smoothRadius; z < res - smoothRadius; z++)
                {
                    for (int x = smoothRadius; x < res - smoothRadius; x++)
                    {
                        // 只平滑受影響區域的邊緣（過渡帶）
                        if (!IsEdgeOfAffected(affected, x, z, res)) continue;

                        float sum = 0f;
                        float weightSum = 0f;

                        for (int dz = -smoothRadius; dz <= smoothRadius; dz++)
                        {
                            for (int dx = -smoothRadius; dx <= smoothRadius; dx++)
                            {
                                float distSq = dx * dx + dz * dz;
                                float sigma = smoothRadius * 0.5f;
                                float weight = Mathf.Exp(-distSq / (2f * sigma * sigma));
                                sum += heights[z + dz, x + dx] * weight;
                                weightSum += weight;
                            }
                        }

                        smoothed[z, x] = sum / weightSum;
                    }
                }

                heights = smoothed;

                EditorUtility.DisplayProgressBar("Terrain Track Conform",
                    $"平滑中 {iter + 1}/{smoothIterations}", 0.8f + 0.2f * ((float)(iter + 1) / smoothIterations));
            }
        }

        EditorUtility.ClearProgressBar();

        td.SetHeights(0, 0, heights);
        EditorUtility.SetDirty(terrain);
        Debug.Log($"[TerrainTrackConformer] 完成：取樣 {sampleCount} 點，平滑 {smoothIterations} 次。");
    }

    bool IsEdgeOfAffected(bool[,] affected, int x, int z, int res)
    {
        if (!affected[z, x]) return false;

        // 檢查鄰居是否有未受影響的點（代表是邊緣）
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx;
                int nz = z + dz;
                if (nx < 0 || nx >= res || nz < 0 || nz >= res) continue;
                if (!affected[nz, nx]) return true;
            }
        }
        return false;
    }

    int WorldToIdx(float world, float terrainOrigin, float terrainSize, int res)
    {
        return Mathf.RoundToInt((world - terrainOrigin) / terrainSize * (res - 1));
    }
}
