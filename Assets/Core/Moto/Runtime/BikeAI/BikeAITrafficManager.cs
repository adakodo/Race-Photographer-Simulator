using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BikeAITrafficManager : MonoBehaviour
{
    /// <summary>比賽階段。</summary>
    public enum RacePhase
    {
        /// <summary>比賽尚未開始（預留給未來起跑排位用）。</summary>
        PreRace,
        /// <summary>開賽衝刺：集團密集、超車頻繁。</summary>
        EarlyRace,
        /// <summary>中段穩定：集團拉開、形成節奏。</summary>
        MidRace,
        /// <summary>尾段衝刺：追趕加成增大、超車更積極。</summary>
        LateRace,
        /// <summary>領先車已完賽。</summary>
        Finished
    }

    public static BikeAITrafficManager Instance { get; private set; }

    [Header("Race Settings — 比賽設定")]
    [Tooltip("比賽總圈數（0 = 無限繞圈，不觸發完賽）")]
    [SerializeField] private int totalLaps = 5;
    [Tooltip("開賽衝刺階段持續的圈數")]
    [SerializeField] private int earlyPhaseLaps = 1;
    [Tooltip("尾段衝刺階段持續的圈數")]
    [SerializeField] private int latePhaseLaps = 1;

    private readonly List<BikeAITrackAgent> agents = new List<BikeAITrackAgent>();
    private readonly List<BikeAITrackAgent> sortedAgents = new List<BikeAITrackAgent>();

    /// <summary>按名次排序的 agent 列表（索引 0 = 第一名）。</summary>
    private readonly List<BikeAITrackAgent> rankedAgents = new List<BikeAITrackAgent>();

    private RacePhase currentPhase = RacePhase.EarlyRace;

    public IReadOnlyList<BikeAITrackAgent> Agents => agents;
    public IReadOnlyList<BikeAITrackAgent> SortedAgents => sortedAgents;
    /// <summary>按名次排序（索引 0 = P1）。每幀更新。</summary>
    public IReadOnlyList<BikeAITrackAgent> RankedAgents => rankedAgents;
    /// <summary>目前比賽階段。</summary>
    public RacePhase CurrentPhase => currentPhase;
    /// <summary>比賽總圈數。</summary>
    public int TotalLaps => totalLaps;
    /// <summary>領先車的圈數。</summary>
    public int LeaderLap => rankedAgents.Count > 0 ? rankedAgents[0].CurrentLap : 0;
    /// <summary>比賽進度 0~1（基於領先車）。totalLaps = 0 時永遠回傳 0.5（中段）。</summary>
    public float RaceProgress => totalLaps > 0 && rankedAgents.Count > 0
        ? Mathf.Clamp01(rankedAgents[0].TotalRaceDistance / (totalLaps * rankedAgents[0].Track.Length))
        : 0.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void LateUpdate()
    {
        SortAgents();
        UpdateRanking();
        UpdateRacePhase();
    }

    public void Register(BikeAITrackAgent agent)
    {
        if (agent == null)
            return;

        if (agents.Contains(agent))
            return;

        agents.Add(agent);
        sortedAgents.Add(agent);
    }

    public void Unregister(BikeAITrackAgent agent)
    {
        if (agent == null)
            return;

        agents.Remove(agent);
        sortedAgents.Remove(agent);
    }

    public BikeAITrackAgent GetFrontAgent(BikeAITrackAgent current, out float forwardGap)
    {
        forwardGap = float.MaxValue;

        if (current == null || current.Track == null || sortedAgents.Count <= 1)
            return null;

        TrackSplineProvider track = current.Track;
        float trackLength = track.Length;
        if (trackLength <= 0f)
            return null;

        BikeAITrackAgent best = null;
        float bestRawGap = float.MaxValue;

        for (int i = 0; i < sortedAgents.Count; i++)
        {
            BikeAITrackAgent other = sortedAgents[i];
            if (other == null || other == current || other.Track != track)
                continue;

            float rawGap = other.DistanceOnTrack - current.DistanceOnTrack;
            if (rawGap <= 0f)
                rawGap += trackLength;

            if (rawGap < bestRawGap)
            {
                bestRawGap = rawGap;
                best = other;
            }
        }

        // 回傳 edge-to-edge 間距（扣除雙方半車長）
        if (best != null)
            forwardGap = bestRawGap - (current.BikeLength + best.BikeLength) * 0.5f;
        else
            forwardGap = bestRawGap;

        return best;
    }

    /// <summary>取得正後方最近的 agent 與間距。</summary>
    public BikeAITrackAgent GetRearAgent(BikeAITrackAgent current, out float rearGap)
    {
        rearGap = float.MaxValue;

        if (current == null || current.Track == null || sortedAgents.Count <= 1)
            return null;

        TrackSplineProvider track = current.Track;
        float trackLength = track.Length;
        if (trackLength <= 0f)
            return null;

        BikeAITrackAgent best = null;
        float bestRawGap = float.MaxValue;

        for (int i = 0; i < sortedAgents.Count; i++)
        {
            BikeAITrackAgent other = sortedAgents[i];
            if (other == null || other == current || other.Track != track)
                continue;

            // 後方間距 = 自己在前，other 在後
            float rawGap = current.DistanceOnTrack - other.DistanceOnTrack;
            if (rawGap <= 0f)
                rawGap += trackLength;

            if (rawGap < bestRawGap)
            {
                bestRawGap = rawGap;
                best = other;
            }
        }

        // 回傳 edge-to-edge 間距（扣除雙方半車長）
        if (best != null)
            rearGap = bestRawGap - (current.BikeLength + best.BikeLength) * 0.5f;
        else
            rearGap = bestRawGap;

        return best;
    }

    /// <summary>
    /// 查詢是否有 agent 正在超車目標為 target（處於 Preparing 或 Overtaking 狀態）。
    /// 回傳最近的攻擊者，無則回傳 null。
    /// </summary>
    public BikeAITrackAgent GetAttacker(BikeAITrackAgent target)
    {
        if (target == null)
            return null;

        for (int i = 0; i < agents.Count; i++)
        {
            BikeAITrackAgent other = agents[i];
            if (other == null || other == target)
                continue;

            if (other.OvertakeTarget == target &&
                (other.CurrentOvertakeState == BikeAITrackAgent.OvertakeState.Preparing ||
                 other.CurrentOvertakeState == BikeAITrackAgent.OvertakeState.Overtaking))
                return other;
        }

        return null;
    }

    public bool IsOffsetBlocked(BikeAITrackAgent current, float targetOffset, float checkGap, float offsetTolerance)
    {
        if (current == null || current.Track == null)
            return false;

        TrackSplineProvider track = current.Track;
        float trackLength = track.Length;

        for (int i = 0; i < agents.Count; i++)
        {
            BikeAITrackAgent other = agents[i];
            if (other == null || other == current || other.Track != track)
                continue;

            // 縱向：edge-to-edge 間距（扣除雙方半車長）
            float rawGap = Mathf.Abs(other.DistanceOnTrack - current.DistanceOnTrack);
            rawGap = Mathf.Min(rawGap, trackLength - rawGap);
            float longitudinalClearance = rawGap - (current.BikeLength + other.BikeLength) * 0.5f;

            if (longitudinalClearance > checkGap)
                continue;

            // 橫向：edge-to-edge 間距（扣除雙方半車寬）
            float lateralClearance = Mathf.Abs(other.CurrentOffset - targetOffset)
                                   - (current.BikeWidth + other.BikeWidth) * 0.5f;

            if (lateralClearance <= offsetTolerance)
                return true;
        }

        return false;
    }

    /// <summary>查詢指定 agent 的當前名次（1 = 第一名）。找不到回傳 -1。</summary>
    public int GetPosition(BikeAITrackAgent agent)
    {
        for (int i = 0; i < rankedAgents.Count; i++)
        {
            if (rankedAgents[i] == agent)
                return i + 1;
        }
        return -1;
    }

    /// <summary>查詢第 position 名的 agent（1 = 第一名）。</summary>
    public BikeAITrackAgent GetAgentAtPosition(int position)
    {
        int idx = position - 1;
        if (idx < 0 || idx >= rankedAgents.Count)
            return null;
        return rankedAgents[idx];
    }

    private void SortAgents()
    {
        sortedAgents.Clear();

        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i] != null && agents[i].Track != null)
                sortedAgents.Add(agents[i]);
        }

        sortedAgents.Sort((a, b) => a.DistanceOnTrack.CompareTo(b.DistanceOnTrack));
    }

    /// <summary>依 TotalRaceDistance 降序排列，距離最大 = 第一名。</summary>
    private void UpdateRanking()
    {
        rankedAgents.Clear();

        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i] != null && agents[i].Track != null)
                rankedAgents.Add(agents[i]);
        }

        rankedAgents.Sort((a, b) => b.TotalRaceDistance.CompareTo(a.TotalRaceDistance));
    }

    /// <summary>根據領先車的圈數判斷比賽階段。</summary>
    private void UpdateRacePhase()
    {
        if (currentPhase == RacePhase.Finished)
            return;

        if (totalLaps <= 0)
        {
            // 無限圈數模式：永遠保持中段
            currentPhase = RacePhase.MidRace;
            return;
        }

        int leaderLap = LeaderLap;

        // 領先車完成所有圈數
        if (leaderLap >= totalLaps)
        {
            currentPhase = RacePhase.Finished;
            return;
        }

        // 開賽衝刺：前 earlyPhaseLaps 圈
        if (leaderLap < earlyPhaseLaps)
        {
            currentPhase = RacePhase.EarlyRace;
            return;
        }

        // 尾段衝刺：最後 latePhaseLaps 圈
        if (leaderLap >= totalLaps - latePhaseLaps)
        {
            currentPhase = RacePhase.LateRace;
            return;
        }

        currentPhase = RacePhase.MidRace;
    }
}