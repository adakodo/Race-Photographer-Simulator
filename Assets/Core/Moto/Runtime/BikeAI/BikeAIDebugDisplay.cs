using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// BikeAI 偵錯視覺化工具。
/// 掛在場景中任意 GameObject 上（建議掛在 BikeAITrafficManager 同物件），
/// Play 模式下在 Scene View 顯示每台車的即時狀態：速度、超車狀態、前車間距、偏移等。
/// 用於診斷超車不觸發、車輛重疊等問題。
/// </summary>
public class BikeAIDebugDisplay : MonoBehaviour
{
    [Header("顯示控制")]
    [Tooltip("啟用 Scene View 偵錯標籤")]
    [SerializeField] private bool showLabels = true;
    [Tooltip("啟用超車觸發條件詳細日誌（Console 輸出）")]
    [SerializeField] private bool logOvertakeConditions = false;
    [Tooltip("標籤顯示高度偏移（公尺）")]
    [SerializeField] private float labelHeightOffset = 2.5f;

    [Header("Gizmo 顏色")]
    [SerializeField] private Color colorNone = Color.white;
    [SerializeField] private Color colorPreparing = Color.yellow;
    [SerializeField] private Color colorOvertaking = new Color(1f, 0.5f, 0f, 1f);
    [SerializeField] private Color colorReturning = Color.cyan;

    /// <summary>上一次日誌輸出時間，避免每幀刷屏。</summary>
    private float lastLogTime;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showLabels || !Application.isPlaying)
            return;

        BikeAITrafficManager traffic = BikeAITrafficManager.Instance;
        if (traffic == null)
            return;

        var agents = traffic.Agents;
        if (agents == null || agents.Count == 0)
            return;

        // 每 0.5 秒輸出一次日誌（避免 Console 刷屏）
        bool shouldLog = logOvertakeConditions && Time.time - lastLogTime > 0.5f;
        if (shouldLog)
            lastLogTime = Time.time;

        for (int i = 0; i < agents.Count; i++)
        {
            BikeAITrackAgent agent = agents[i];
            if (agent == null)
                continue;

            DrawAgentGizmo(agent, traffic, shouldLog);
        }
    }

    private void DrawAgentGizmo(BikeAITrackAgent agent, BikeAITrafficManager traffic, bool shouldLog)
    {
        // --- 狀態顏色 ---
        Color stateColor;
        string stateName;
        switch (agent.CurrentOvertakeState)
        {
            case BikeAITrackAgent.OvertakeState.Preparing:
                stateColor = colorPreparing;
                stateName = "準備超車";
                break;
            case BikeAITrackAgent.OvertakeState.Overtaking:
                stateColor = colorOvertaking;
                stateName = "超車中";
                break;
            case BikeAITrackAgent.OvertakeState.Returning:
                stateColor = colorReturning;
                stateName = "回歸走線";
                break;
            default:
                stateColor = colorNone;
                stateName = "正常";
                break;
        }

        // --- 前車間距 ---
        BikeAITrackAgent frontAgent = traffic.GetFrontAgent(agent, out float frontGap);
        string frontInfo = frontAgent != null
            ? $"前車: {frontGap:F1}m (速:{frontAgent.CurrentSpeed:F1})"
            : "前車: 無";

        // --- 名次 ---
        int position = traffic.GetPosition(agent);

        // --- 標籤文字 ---
        Vector3 labelPos = agent.transform.position + Vector3.up * labelHeightOffset;

        string label = $"P{position} | {stateName}\n" +
                       $"速度: {agent.CurrentSpeed:F1} / 目標: {agent.TargetSpeed:F1}\n" +
                       $"偏移: {agent.CurrentOffset:F2} → {agent.TargetOffset:F2}\n" +
                       $"{frontInfo}";

        // --- 繪製車體範圍框（依車體尺寸）---
        Gizmos.color = stateColor;
        Vector3 boxSize = new Vector3(agent.BikeWidth, 0.3f, agent.BikeLength);
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(agent.transform.position + Vector3.up * 0.5f,
            agent.transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, boxSize);
        Gizmos.matrix = oldMatrix;

        // --- 前車連線（紅=太近、黃=超車距離內、綠=安全）---
        if (frontAgent != null)
        {
            Gizmos.color = frontGap < 3f ? Color.red : (frontGap < 8f ? Color.yellow : Color.green);
            Gizmos.DrawLine(agent.transform.position + Vector3.up * 0.5f,
                           frontAgent.transform.position + Vector3.up * 0.5f);
        }

        // --- 標籤 ---
        GUIStyle style = new GUIStyle();
        style.normal.textColor = stateColor;
        style.fontSize = 11;
        style.fontStyle = FontStyle.Bold;
        Handles.Label(labelPos, label, style);

        // --- 超車條件日誌（含阻塞檢測）---
        // 注意：超車判斷用的是巡航速度（不受彎道減速影響），不是最終 TargetSpeed
        if (shouldLog && frontAgent != null)
        {
            bool gapOk = frontGap < 8f;

            if (gapOk || frontGap < 15f)
            {
                // 檢測左右側是否被阻塞（與 ApplyTrafficLogic 相同邏輯）
                string blockInfo = "";
                if (gapOk && agent.CurrentOvertakeState == BikeAITrackAgent.OvertakeState.None)
                {
                    float leftOffset = agent.CurrentOffset - 1.5f;
                    float rightOffset = agent.CurrentOffset + 1.5f;
                    bool leftBlocked = traffic.IsOffsetBlocked(agent, leftOffset, 8f, 0.8f);
                    bool rightBlocked = traffic.IsOffsetBlocked(agent, rightOffset, 8f, 0.8f);
                    blockInfo = $", 左阻塞={leftBlocked}, 右阻塞={rightBlocked}";

                    if (leftBlocked && rightBlocked)
                        blockInfo += " ★兩側都被擋！";
                }

                Debug.Log($"[BikeAI] {agent.name} P{position}: " +
                          $"狀態={stateName}, 間距={frontGap:F1}m, " +
                          $"最終速={agent.TargetSpeed:F1}, 前車速={frontAgent.CurrentSpeed:F1}, " +
                          $"maxSpd={agent.MaxSpeed:F1}" +
                          $"{blockInfo}, " +
                          $"偏移={agent.CurrentOffset:F2}→{agent.TargetOffset:F2}",
                          agent);
            }
        }
    }
#endif
}
