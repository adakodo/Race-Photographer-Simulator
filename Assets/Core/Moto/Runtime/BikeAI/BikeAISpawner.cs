using UnityEngine;

/// <summary>
/// 在賽道上自動生成 AI 機車並分配集團。
/// 放置在場景中任意 GameObject 上，指定 prefab 和 track 即可。
/// </summary>
public class BikeAISpawner : MonoBehaviour
{
    [Header("References")]
    [Tooltip("AI 機車 Prefab（需含 BikeAITrackAgent + BikeAIStrategy_Follow）")]
    [SerializeField] private GameObject bikePrefab;
    [Tooltip("賽道 spline provider")]
    [SerializeField] private TrackSplineProvider track;

    [Header("Spawn Settings")]
    [Tooltip("生成的機車數量")]
    [SerializeField] private int bikeCount = 10;
    [Tooltip("第一台車的起始距離（公尺），預設 0 = 賽道起點")]
    [SerializeField] private float startOffset = 0f;
    [Tooltip("車與車之間的間距（公尺）。0 = 自動均勻分佈整條賽道")]
    [SerializeField] private float spacing = 0f;

    [Header("Lateral Spread — 開場橫向散佈")]
    [Tooltip("開場時每台車的隨機橫向偏移範圍（±公尺），避免全部擠在同一線")]
    [SerializeField] private float initialOffsetSpread = 0.6f;

    [Header("Pack Assignment — 集團分配")]
    [Tooltip("集團數量，需與 Strategy 的 numberOfPacks 一致")]
    [SerializeField] private int numberOfPacks = 3;
    [Tooltip("各集團的車數比例（由快到慢）。長度需等於 numberOfPacks。留空則平均分配。")]
    [SerializeField] private int[] packDistribution;

    private void Start()
    {
        if (bikePrefab == null || track == null || !track.IsValid())
        {
            Debug.LogWarning("[BikeAISpawner] bikePrefab 或 track 未設定或無效，跳過生成。");
            return;
        }

        SpawnBikes();
    }

    private void SpawnBikes()
    {
        float trackLength = track.Length;
        // 計算間距：若未指定則均勻分佈
        float actualSpacing = spacing > 0f ? spacing : trackLength / bikeCount;

        // 建立集團分配表
        int[] packAssignments = BuildPackAssignments();

        for (int i = 0; i < bikeCount; i++)
        {
            float dist = track.WrapDistance(startOffset + i * actualSpacing);

            GameObject bike = Instantiate(bikePrefab, transform);
            bike.name = $"BikeAI_{i:D2}";

            // 注入賽道與起始位置（含隨機橫向散佈）
            BikeAITrackAgent agent = bike.GetComponent<BikeAITrackAgent>();
            if (agent != null)
            {
                float lateralOffset = Random.Range(-initialOffsetSpread, initialOffsetSpread);
                agent.InitTrack(track, dist, lateralOffset);
            }

            // 分配集團
            BikeAIStrategy_Follow strategy = bike.GetComponent<BikeAIStrategy_Follow>();
            if (strategy != null)
            {
                strategy.SetAssignedPack(packAssignments[i]);
            }
        }
    }

    /// <summary>
    /// 根據 packDistribution 或平均分配，為每台車產生集團索引。
    /// 回傳陣列長度 = bikeCount，值為 0 ~ numberOfPacks-1。
    /// </summary>
    private int[] BuildPackAssignments()
    {
        int[] assignments = new int[bikeCount];
        numberOfPacks = Mathf.Max(1, numberOfPacks);

        // 使用自訂比例分配
        if (packDistribution != null && packDistribution.Length == numberOfPacks)
        {
            int idx = 0;
            for (int pack = 0; pack < numberOfPacks; pack++)
            {
                int count = packDistribution[pack];
                for (int j = 0; j < count && idx < bikeCount; j++)
                {
                    assignments[idx] = pack;
                    idx++;
                }
            }
            // 剩餘的車分配到最後一個集團
            for (; idx < bikeCount; idx++)
            {
                assignments[idx] = numberOfPacks - 1;
            }
        }
        else
        {
            // 平均分配：依序輪流指定集團
            for (int i = 0; i < bikeCount; i++)
            {
                assignments[i] = i % numberOfPacks;
            }
        }

        // 打亂順序，讓同集團的車不會全部排在一起起跑
        for (int i = assignments.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = assignments[i];
            assignments[i] = assignments[j];
            assignments[j] = temp;
        }

        return assignments;
    }
}
