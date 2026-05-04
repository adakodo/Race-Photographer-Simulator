using UnityEngine;

[DisallowMultipleComponent]
public class BikeAIStrategy_Follow : MonoBehaviour
{
    [Header("Base")]
    [SerializeField] private float cruiseSpeed = 14f;
    [SerializeField] private float defaultOffset = 0f;

    [Header("Follow")]
    [SerializeField] private float followDistance = 5f;
    [SerializeField] private float brakeDistance = 3f;
    [SerializeField] private float minFollowSpeed = 4f;

    [Header("Curve — 彎道速度控制")]
    [SerializeField] private float curveLookAhead = 12f;
    [SerializeField] private float curveAngleForFullSlowdown = 50f;
    [SerializeField] private float minCurveSpeedRatio = 0.7f;
    [Tooltip("短距離前視（公尺），偵測是否正在出彎")]
    [SerializeField] private float curveExitLookBehind = 4f;
    [Tooltip("出彎加速倍率，讓車在彎道出口明顯加速")]
    [SerializeField] private float exitBoostRatio = 1.15f;

    [Header("Racing Line — 彎道走線")]
    [SerializeField] private bool enableRacingLine = true;
    [Tooltip("走線最大橫向偏移（公尺）")]
    [SerializeField] private float racingLineWidth = 1.2f;
    [Tooltip("近距離前視（公尺），偵測當前彎道用於 apex 內切")]
    [SerializeField] private float racingLineApexLookAhead = 6f;
    [Tooltip("遠距離前視（公尺），偵測前方彎道用於提前卡外線")]
    [SerializeField] private float racingLineEntryLookAhead = 25f;
    [Tooltip("曲率參考值（度/公尺），達到此值時偏移到最大寬度")]
    [SerializeField] private float racingLineCurvatureRef = 3f;

    [Header("Pack Grouping — 集團分組")]
    [Tooltip("集團數量（例如 3 = 前段/中段/尾段）")]
    [SerializeField] private int numberOfPacks = 3;
    [Tooltip("相鄰集團之間的巡航速度差（m/s）")]
    [SerializeField] private float packSpeedGap = 3f;
    [Tooltip("同集團內的速度隨機範圍（±）")]
    [SerializeField] private float withinPackSpeedSpread = 1f;
    [Tooltip("手動指定集團（-1 = 自動隨機分配，0 = 最快集團）")]
    [SerializeField] private int assignedPack = -1;

    [Header("Individual Variation — 個體差異")]
    [Tooltip("啟用後每台車在 Awake 時自動隨機化參數，消除排隊感")]
    [SerializeField] private bool enableVariation = true;
    [Tooltip("走線寬度隨機偏移範圍（±）")]
    [SerializeField] private float racingLineWidthVariation = 0.4f;
    [Tooltip("apex 前視距離隨機偏移範圍（±），影響早/晚切 apex")]
    [SerializeField] private float apexLookAheadVariation = 3f;
    [Tooltip("預設橫向偏好隨機範圍（±），讓每台車有慣用側")]
    [SerializeField] private float defaultOffsetVariation = 0.4f;
    [Tooltip("彎道最低速度比隨機偏移範圍（±），影響過彎激進程度")]
    [SerializeField] private float curveSpeedRatioVariation = 0.08f;

    [Header("Catch-Up — 追趕邏輯")]
    [Tooltip("啟用追趕邏輯，讓落後車逐漸追上前方集團")]
    [SerializeField] private bool enableCatchUp = true;
    [Tooltip("開始追趕的前車間距（公尺）")]
    [SerializeField] private float catchUpThreshold = 15f;
    [Tooltip("追趕加速達到最大的間距（公尺）")]
    [SerializeField] private float catchUpMaxDistance = 50f;
    [Tooltip("最大追趕速度加成（m/s）")]
    [SerializeField] private float catchUpSpeedBonus = 5f;
    [Tooltip("後車施壓距離（公尺），後車在此距離內時前車會微幅加速防禦")]
    [SerializeField] private float rearPressureDistance = 8f;
    [Tooltip("前車防禦加速（m/s）")]
    [SerializeField] private float rearPressureSpeedBonus = 1.5f;

    [Header("Slipstream — 尾流效應")]
    [Tooltip("啟用尾流效應，跟在前車正後方可獲得速度加成")]
    [SerializeField] private bool enableSlipstream = true;
    [Tooltip("尾流有效最大距離（公尺）")]
    [SerializeField] private float slipstreamMaxDistance = 12f;
    [Tooltip("尾流失效最小距離（公尺），太近則進入亂流區")]
    [SerializeField] private float slipstreamMinDistance = 2f;
    [Tooltip("橫向對齊容差（公尺），偏移差小於此值才算在尾流中")]
    [SerializeField] private float slipstreamOffsetTolerance = 0.8f;
    [Tooltip("最大尾流速度加成（m/s）")]
    [SerializeField] private float slipstreamSpeedBonus = 3f;

    [Header("Attack & Defend — 攻防互動")]
    [Tooltip("啟用攻防互動：防禦封線 + 反擊超車")]
    [SerializeField] private bool enableAttackDefend = true;
    [Tooltip("防禦封線強度（0~1），向攻擊者偏移的比例")]
    [SerializeField] private float defenseOffsetStrength = 0.7f;
    [Tooltip("防禦時微幅加速（m/s），避免被輕鬆超越")]
    [SerializeField] private float defenseSpeedBonus = 1.5f;
    [Tooltip("反擊超車持續時間（秒），被超越後進入反擊模式")]
    [SerializeField] private float counterAttackDuration = 3f;
    [Tooltip("反擊時額外速度加成（m/s）")]
    [SerializeField] private float counterAttackSpeedBonus = 2f;
    [Tooltip("攻防激進度（0=保守, 1=激進），影響防禦強度與反擊意願")]
    [SerializeField] private float aggressiveness = 0.5f;
    [Tooltip("激進度隨機範圍（±），個體差異用")]
    [SerializeField] private float aggressivenessVariation = 0.3f;

    [Header("Lateral Avoidance — 橫向避讓")]
    [Tooltip("啟用橫向避讓，防止車輛重疊")]
    [SerializeField] private bool enableLateralAvoidance = true;
    [Tooltip("縱向檢測距離（公尺），在此距離內的車才會互相推開")]
    [SerializeField] private float lateralAvoidDistance = 4f;
    [Tooltip("最小橫向間距（公尺），低於此值開始推開")]
    [SerializeField] private float lateralMinSeparation = 0.8f;
    [Tooltip("推開力度，值越大反應越強烈")]
    [SerializeField] private float lateralAvoidStrength = 2f;

    [Header("Race Phase — 賽段動態")]
    [Tooltip("啟用賽段動態，根據比賽階段自動調節行為參數")]
    [SerializeField] private bool enableRacePhase = true;
    [Tooltip("開賽衝刺：超車觸發距離倍率（> 1 = 更積極超車）")]
    [SerializeField] private float earlyOvertakeDistanceMultiplier = 1.5f;
    [Tooltip("開賽衝刺：追趕加成倍率")]
    [SerializeField] private float earlyCatchUpMultiplier = 0.5f;
    [Tooltip("尾段衝刺：超車觸發距離倍率")]
    [SerializeField] private float lateOvertakeDistanceMultiplier = 1.3f;
    [Tooltip("尾段衝刺：追趕加成倍率（> 1 = 更強追趕）")]
    [SerializeField] private float lateCatchUpMultiplier = 1.5f;
    [Tooltip("尾段衝刺：超車速度加成倍率")]
    [SerializeField] private float lateOvertakeSpeedMultiplier = 1.3f;

    [Header("Overtake")]
    [SerializeField] private bool enableOvertake = true;
    [SerializeField] private float overtakeOffset = 1.5f;
    [SerializeField] private float overtakeTriggerDistance = 8f;
    [SerializeField] private float overtakeCompleteDistance = 2.5f;
    [SerializeField] private float returnOffsetThreshold = 0.15f;
    [SerializeField] private float laneCheckGap = 8f;
    [SerializeField] private float laneOffsetTolerance = 0.8f;
    [SerializeField] private float overtakeSpeedBonus = 3f;

    /// <summary>運行時分配的集團索引（0 = 最快）。</summary>
    private int runtimePack;
    private bool initialized;

    /// <summary>上一次偵測到的攻擊者，用於判斷「剛被超越」觸發反擊。</summary>
    private BikeAITrackAgent lastAttacker;
    /// <summary>反擊超車計時器，大於 0 表示處於反擊模式。</summary>
    private float counterAttackTimer;

    // --- 賽段動態運行時倍率（每次 Tick 開頭更新）---
    private float phaseOvertakeDistanceMul = 1f;
    private float phaseCatchUpMul = 1f;
    private float phaseOvertakeSpeedMul = 1f;

    /// <summary>由 Spawner 呼叫，在 Start 之前設定集團。</summary>
    public void SetAssignedPack(int pack)
    {
        assignedPack = pack;
    }

    private void Start()
    {
        if (initialized) return;
        ApplyVariation();
    }

    /// <summary>初始化集團分組與個體差異。可由外部提前呼叫。</summary>
    public void ApplyVariation()
    {
        if (initialized) return;
        initialized = true;

        // --- 集團分組：決定巡航速度等級 ---
        numberOfPacks = Mathf.Max(1, numberOfPacks);
        runtimePack = assignedPack >= 0 ? Mathf.Clamp(assignedPack, 0, numberOfPacks - 1)
                                        : Random.Range(0, numberOfPacks);

        // 最快集團 (0) 的速度 = cruiseSpeed，之後每組遞減 packSpeedGap
        // 組內再加小範圍隨機，讓同集團的車速度接近但不完全相同
        cruiseSpeed -= runtimePack * packSpeedGap;
        cruiseSpeed += Random.Range(-withinPackSpeedSpread, withinPackSpeedSpread);

        if (!enableVariation)
        {
            cruiseSpeed = Mathf.Max(cruiseSpeed, 10f);
            return;
        }

        // --- 個體差異：走線、apex、偏好、過彎風格 ---
        racingLineWidth += Random.Range(-racingLineWidthVariation, racingLineWidthVariation);
        racingLineApexLookAhead += Random.Range(-apexLookAheadVariation, apexLookAheadVariation);
        defaultOffset += Random.Range(-defaultOffsetVariation, defaultOffsetVariation);
        minCurveSpeedRatio += Random.Range(-curveSpeedRatioVariation, curveSpeedRatioVariation);

        // 攻防激進度隨機化
        aggressiveness += Random.Range(-aggressivenessVariation, aggressivenessVariation);
        aggressiveness = Mathf.Clamp01(aggressiveness);

        // 確保數值合理
        cruiseSpeed = Mathf.Max(cruiseSpeed, 10f);
        racingLineWidth = Mathf.Max(racingLineWidth, 0.2f);
        racingLineApexLookAhead = Mathf.Max(racingLineApexLookAhead, 2f);
        minCurveSpeedRatio = Mathf.Clamp(minCurveSpeedRatio, 0.5f, 0.9f);
    }

    public void Tick(BikeAITrackAgent agent)
    {
        if (agent == null || agent.Track == null || !agent.Track.IsValid())
            return;

        UpdatePhaseMultipliers();

        float desiredSpeed = Mathf.Min(cruiseSpeed, agent.MaxSpeed);

        // 計算 racing line 偏移，疊加個體偏好（defaultOffset 含隨機偏移）
        float baseOffset = defaultOffset + (enableRacingLine ? ComputeRacingLineOffset(agent) : 0f);
        float desiredOffset = baseOffset;

        ApplyCurveSlowdown(agent, ref desiredSpeed);
        ApplyTrafficLogic(agent, ref desiredSpeed, ref desiredOffset, baseOffset);
        ApplyCatchUp(agent, ref desiredSpeed);
        ApplySlipstream(agent, ref desiredSpeed);
        ApplyAttackDefend(agent, ref desiredSpeed, ref desiredOffset);
        ApplyLateralAvoidance(agent, ref desiredOffset);

        agent.SetTargetSpeed(desiredSpeed);
        agent.SetTargetOffset(desiredOffset);
    }

    /// <summary>
    /// 根據賽道曲率計算 racing line 橫向偏移（外→內→外）。
    /// 近距離曲率驅動 apex 內切，遠距離曲率驅動入彎外卡。
    /// </summary>
    private float ComputeRacingLineOffset(BikeAITrackAgent agent)
    {
        TrackSplineProvider track = agent.Track;
        float d = agent.DistanceOnTrack;

        var sampleCur = track.GetSampleByDistance(d);
        var sampleApex = track.GetSampleByDistance(d + racingLineApexLookAhead);
        var sampleEntry = track.GetSampleByDistance(d + racingLineEntryLookAhead);

        // 角速率（度/公尺）：標準化不同前視距離的曲率
        float apexAngle = Vector3.SignedAngle(sampleCur.forward, sampleApex.forward, sampleCur.up);
        float entryAngle = Vector3.SignedAngle(sampleCur.forward, sampleEntry.forward, sampleCur.up);
        float apexRate = apexAngle / racingLineApexLookAhead;
        float entryRate = entryAngle / racingLineEntryLookAhead;

        // apex：在彎中走內線（曲率方向的反方向偏移）
        float apexFactor = Mathf.Clamp(-apexRate / racingLineCurvatureRef, -1f, 1f);
        // entry：前方有彎道時提前走外線（曲率方向的同方向偏移）
        float entryFactor = Mathf.Clamp(entryRate / racingLineCurvatureRef, -1f, 1f);

        // 合成：apex 為主（0.7），入彎預判為輔（0.3）
        float combined = Mathf.Clamp(apexFactor * 0.7f + entryFactor * 0.3f, -1f, 1f);
        return combined * racingLineWidth;
    }

    private void ApplyCurveSlowdown(BikeAITrackAgent agent, ref float desiredSpeed)
    {
        TrackSplineProvider track = agent.Track;
        float d0 = agent.DistanceOnTrack;

        // --- 前方彎道偵測（煞車判斷）---
        float d1 = track.WrapDistance(d0 + curveLookAhead * 0.5f);
        float d2 = track.WrapDistance(d0 + curveLookAhead);

        Vector3 f0 = track.GetSampleByDistance(d0).forward;
        Vector3 f1 = track.GetSampleByDistance(d1).forward;
        Vector3 f2 = track.GetSampleByDistance(d2).forward;

        float angleA = Vector3.Angle(f0, f1);
        float angleB = Vector3.Angle(f1, f2);
        float aheadCurve = Mathf.Max(angleA, angleB);

        float t = Mathf.InverseLerp(0f, curveAngleForFullSlowdown, aheadCurve);
        float speedRatio = Mathf.Lerp(1f, minCurveSpeedRatio, t);

        // --- 超車晚煞車：Preparing/Overtaking 狀態時允許更高彎道速度（模擬 dive-bomb）---
        bool isOvertaking = agent.CurrentOvertakeState == BikeAITrackAgent.OvertakeState.Preparing
                         || agent.CurrentOvertakeState == BikeAITrackAgent.OvertakeState.Overtaking;
        if (isOvertaking)
        {
            float lateBrakeFactor = Mathf.Lerp(0.05f, 0.15f, aggressiveness);
            speedRatio = Mathf.Min(1f, speedRatio + lateBrakeFactor);
        }

        // --- 出彎偵測：後方曲率大、前方曲率小 → 正在出彎，給予加速 ---
        float dBehind = track.WrapDistance(d0 - curveExitLookBehind);
        Vector3 fBehind = track.GetSampleByDistance(dBehind).forward;
        float behindAngle = Vector3.Angle(fBehind, f0);

        // 短距離前方曲率（判斷是否已脫離彎道）
        float nearAheadAngle = angleA;

        // 出彎條件：後方曲率顯著 且 前方已趨直
        bool isExitingCorner = behindAngle > 5f && nearAheadAngle < 3f;

        if (isExitingCorner)
        {
            // 出彎加速：速度乘以加速倍率，但不超過巡航速度
            desiredSpeed = Mathf.Min(desiredSpeed * exitBoostRatio, cruiseSpeed);
        }
        else
        {
            desiredSpeed *= speedRatio;
        }
    }

    private void ApplyTrafficLogic(BikeAITrackAgent agent, ref float desiredSpeed, ref float desiredOffset, float baseOffset)
    {
        BikeAITrafficManager traffic = BikeAITrafficManager.Instance;
        if (traffic == null)
        {
            agent.SetOvertakeState(BikeAITrackAgent.OvertakeState.None, null);
            return;
        }

        BikeAITrackAgent frontAgent = traffic.GetFrontAgent(agent, out float gap);
        if (frontAgent == null)
        {
            agent.SetOvertakeState(BikeAITrackAgent.OvertakeState.None, null);
            desiredOffset = baseOffset;
            return;
        }

        if (gap <= brakeDistance)
        {
            desiredSpeed = Mathf.Min(desiredSpeed, Mathf.Max(minFollowSpeed, frontAgent.CurrentSpeed * 0.6f));
        }
        else if (gap <= followDistance)
        {
            desiredSpeed = Mathf.Min(desiredSpeed, Mathf.Max(minFollowSpeed, frontAgent.CurrentSpeed));
        }

        if (!enableOvertake)
        {
            agent.SetOvertakeState(BikeAITrackAgent.OvertakeState.None, null);
            desiredOffset = baseOffset;
            return;
        }

        // 超車意願速度：用巡航速度（不受彎道減速影響）判斷「我是否本來就比前車快」
        float overtakeIntentSpeed = Mathf.Min(cruiseSpeed, agent.MaxSpeed);

        switch (agent.CurrentOvertakeState)
        {
            case BikeAITrackAgent.OvertakeState.None:
                {
                    // 賽段倍率：開賽/尾段時超車觸發距離更大（更積極）
                    float effectiveTriggerDist = overtakeTriggerDistance * phaseOvertakeDistanceMul;
                    float effectiveOvertakeBonus = overtakeSpeedBonus * phaseOvertakeSpeedMul;

                    if (gap < effectiveTriggerDist && frontAgent.CurrentSpeed < overtakeIntentSpeed)
                    {
                        // 激進車手超車偏移較小（切更近），保守車手偏移較大（繞更遠）
                        float effectiveOvertakeOffset = overtakeOffset * Mathf.Lerp(1.2f, 0.8f, aggressiveness);
                        float leftOffset = baseOffset - effectiveOvertakeOffset;
                        float rightOffset = baseOffset + effectiveOvertakeOffset;

                        // 依個體偏好決定優先超車側（偏左的車優先左超、偏右優先右超）
                        bool preferLeft = defaultOffset <= 0f;
                        float firstOffset = preferLeft ? leftOffset : rightOffset;
                        float secondOffset = preferLeft ? rightOffset : leftOffset;

                        bool firstBlocked = traffic.IsOffsetBlocked(agent, firstOffset, laneCheckGap, laneOffsetTolerance);
                        bool secondBlocked = traffic.IsOffsetBlocked(agent, secondOffset, laneCheckGap, laneOffsetTolerance);

                        if (!firstBlocked)
                        {
                            desiredOffset = firstOffset;
                            desiredSpeed = Mathf.Min(agent.MaxSpeed, desiredSpeed + effectiveOvertakeBonus);
                            agent.SetOvertakeState(BikeAITrackAgent.OvertakeState.Preparing, frontAgent);
                        }
                        else if (!secondBlocked)
                        {
                            desiredOffset = secondOffset;
                            desiredSpeed = Mathf.Min(agent.MaxSpeed, desiredSpeed + effectiveOvertakeBonus);
                            agent.SetOvertakeState(BikeAITrackAgent.OvertakeState.Preparing, frontAgent);
                        }
                        else
                        {
                            desiredOffset = baseOffset;
                        }
                    }
                    else
                    {
                        desiredOffset = baseOffset;
                    }

                    break;
                }

            case BikeAITrackAgent.OvertakeState.Preparing:
                {
                    desiredSpeed = Mathf.Min(agent.MaxSpeed, desiredSpeed + overtakeSpeedBonus * phaseOvertakeSpeedMul);
                    desiredOffset = agent.TargetOffset;

                    if (Mathf.Abs(agent.CurrentOffset - agent.TargetOffset) < 0.2f)
                    {
                        agent.SetOvertakeState(BikeAITrackAgent.OvertakeState.Overtaking, agent.OvertakeTarget);
                    }

                    break;
                }

            case BikeAITrackAgent.OvertakeState.Overtaking:
                {
                    desiredSpeed = Mathf.Min(agent.MaxSpeed, desiredSpeed + overtakeSpeedBonus * phaseOvertakeSpeedMul);
                    desiredOffset = agent.TargetOffset;

                    BikeAITrackAgent target = agent.OvertakeTarget;
                    if (target == null)
                    {
                        agent.SetOvertakeState(BikeAITrackAgent.OvertakeState.Returning, null);
                    }
                    else
                    {
                        // edge-to-edge 間距（扣除雙方半車長）
                        float rawPassGap = agent.DistanceOnTrack - target.DistanceOnTrack;
                        if (rawPassGap < 0f)
                            rawPassGap += agent.Track.Length;
                        float passGap = rawPassGap - (agent.BikeLength + target.BikeLength) * 0.5f;

                        if (passGap > overtakeCompleteDistance && rawPassGap < agent.Track.Length * 0.5f)
                        {
                            agent.SetOvertakeState(BikeAITrackAgent.OvertakeState.Returning, null);
                        }
                    }

                    break;
                }

            case BikeAITrackAgent.OvertakeState.Returning:
                {
                    // 超車完成後回歸 racing line 走線
                    desiredOffset = baseOffset;

                    if (Mathf.Abs(agent.CurrentOffset - baseOffset) <= returnOffsetThreshold)
                    {
                        agent.SetOvertakeState(BikeAITrackAgent.OvertakeState.None, null);
                    }

                    break;
                }
        }
    }

    /// <summary>
    /// 追趕邏輯：落後車漸進加速追上前方集團 + 後車施壓時前車防禦加速。
    /// </summary>
    private void ApplyCatchUp(BikeAITrackAgent agent, ref float desiredSpeed)
    {
        if (!enableCatchUp)
            return;

        BikeAITrafficManager traffic = BikeAITrafficManager.Instance;
        if (traffic == null)
            return;

        // --- 追趕加速：前車間距超過閾值時給予漸進式速度加成（受賽段倍率影響）---
        BikeAITrackAgent frontAgent = traffic.GetFrontAgent(agent, out float frontGap);
        if (frontAgent != null && frontGap > catchUpThreshold)
        {
            // 間距越大加成越高，在 catchUpMaxDistance 時達到最大
            float catchUpRatio = Mathf.InverseLerp(catchUpThreshold, catchUpMaxDistance, frontGap);
            float bonus = catchUpRatio * catchUpSpeedBonus * phaseCatchUpMul;
            desiredSpeed = Mathf.Min(agent.MaxSpeed, desiredSpeed + bonus);
        }

        // --- 後方壓力：後車接近時前車微幅加速防禦，製造拉鋸感 ---
        BikeAITrackAgent rearAgent = traffic.GetRearAgent(agent, out float rearGap);
        if (rearAgent != null && rearGap < rearPressureDistance)
        {
            // 後車越近壓力越大
            float pressureRatio = 1f - Mathf.InverseLerp(0f, rearPressureDistance, rearGap);
            float bonus = pressureRatio * rearPressureSpeedBonus;
            desiredSpeed = Mathf.Min(agent.MaxSpeed, desiredSpeed + bonus);
        }
    }

    /// <summary>
    /// 尾流效應：跟在前車正後方（橫向對齊）時獲得速度加成，模擬空力尾流。
    /// 有效範圍在 slipstreamMinDistance ~ slipstreamMaxDistance 之間，
    /// 距離越近加成越大，太近則進入亂流區失效。
    /// </summary>
    private void ApplySlipstream(BikeAITrackAgent agent, ref float desiredSpeed)
    {
        if (!enableSlipstream)
            return;

        BikeAITrafficManager traffic = BikeAITrafficManager.Instance;
        if (traffic == null)
            return;

        BikeAITrackAgent frontAgent = traffic.GetFrontAgent(agent, out float gap);
        if (frontAgent == null)
            return;

        // 距離不在有效範圍內則無尾流
        if (gap < slipstreamMinDistance || gap > slipstreamMaxDistance)
            return;

        // 橫向偏移差過大則不在尾流中（扣除雙方半車寬後比較容差）
        float offsetDiff = Mathf.Abs(agent.CurrentOffset - frontAgent.CurrentOffset);
        float combinedHalfWidth = (agent.BikeWidth + frontAgent.BikeWidth) * 0.5f;
        if (offsetDiff - combinedHalfWidth > slipstreamOffsetTolerance)
            return;

        // 距離越近加成越大（minDistance 時最大，maxDistance 時為零）
        float slipRatio = 1f - Mathf.InverseLerp(slipstreamMinDistance, slipstreamMaxDistance, gap);
        float bonus = slipRatio * slipstreamSpeedBonus;
        desiredSpeed = Mathf.Min(agent.MaxSpeed, desiredSpeed + bonus);
    }

    /// <summary>
    /// 攻防互動：
    /// 1. 防禦封線 — 偵測到後車正在超車時，偏移向攻擊者同側封鎖路線並微幅加速。
    /// 2. 反擊超車 — 被超越後進入反擊模式，給予短時間速度加成以利追回。
    /// 兩者皆受 aggressiveness 調節。
    /// </summary>
    private void ApplyAttackDefend(BikeAITrackAgent agent, ref float desiredSpeed, ref float desiredOffset)
    {
        if (!enableAttackDefend)
        {
            lastAttacker = null;
            counterAttackTimer = 0f;
            return;
        }

        BikeAITrafficManager traffic = BikeAITrafficManager.Instance;
        if (traffic == null)
            return;

        // --- 反擊計時器遞減 ---
        if (counterAttackTimer > 0f)
            counterAttackTimer -= Time.deltaTime;

        // --- 防禦封線：偵測是否有人正在超我 ---
        BikeAITrackAgent attacker = traffic.GetAttacker(agent);

        if (attacker != null)
        {
            // 只在自己未處於超車狀態時防禦（避免同時攻防衝突）
            if (agent.CurrentOvertakeState == BikeAITrackAgent.OvertakeState.None)
            {
                // 向攻擊者的偏移方向靠攏，封鎖超車路線
                float attackerOffset = attacker.CurrentOffset;
                float defenseShift = (attackerOffset - agent.CurrentOffset) * defenseOffsetStrength * aggressiveness;
                desiredOffset += defenseShift;

                // 防禦加速：激進度越高加速越多
                desiredSpeed = Mathf.Min(agent.MaxSpeed, desiredSpeed + defenseSpeedBonus * aggressiveness);
            }

            lastAttacker = attacker;
        }
        else
        {
            // 上一幀有攻擊者、這一幀沒有 → 攻擊者已超越我或放棄，觸發反擊
            if (lastAttacker != null)
            {
                // 確認對方確實超過了我（不是放棄超車）
                float passGap = lastAttacker.DistanceOnTrack - agent.DistanceOnTrack;
                if (passGap < 0f && agent.Track != null)
                    passGap += agent.Track.Length;

                // 對方在前方且距離合理，視為被超越 → 啟動反擊
                if (passGap > 0f && passGap < agent.Track.Length * 0.5f)
                {
                    counterAttackTimer = counterAttackDuration * aggressiveness;
                }

                lastAttacker = null;
            }
        }

        // --- 反擊超車：被超越後短時間速度加成，幫助追回並觸發正常超車邏輯 ---
        if (counterAttackTimer > 0f)
        {
            float counterRatio = Mathf.Clamp01(counterAttackTimer / (counterAttackDuration * aggressiveness));
            float counterBonus = counterRatio * counterAttackSpeedBonus * aggressiveness;
            desiredSpeed = Mathf.Min(agent.MaxSpeed, desiredSpeed + counterBonus);
        }
    }

    /// <summary>
    /// 橫向避讓：偵測縱向近距離的車，當橫向間距不足時互相推開，防止視覺重疊。
    /// 作為所有偏移決策之後的最終安全層。
    /// </summary>
    private void ApplyLateralAvoidance(BikeAITrackAgent agent, ref float desiredOffset)
    {
        if (!enableLateralAvoidance)
            return;

        BikeAITrafficManager traffic = BikeAITrafficManager.Instance;
        if (traffic == null)
            return;

        TrackSplineProvider track = agent.Track;
        if (track == null)
            return;

        float trackLength = track.Length;
        float totalPush = 0f;

        var agents = traffic.Agents;
        for (int i = 0; i < agents.Count; i++)
        {
            BikeAITrackAgent other = agents[i];
            if (other == null || other == agent || other.Track != track)
                continue;

            // 縱向距離檢測（edge-to-edge，扣除雙方半車長）
            float rawLongGap = Mathf.Abs(other.DistanceOnTrack - agent.DistanceOnTrack);
            rawLongGap = Mathf.Min(rawLongGap, trackLength - rawLongGap);
            float combinedHalfLength = (agent.BikeLength + other.BikeLength) * 0.5f;
            float effectiveAvoidDist = combinedHalfLength + lateralAvoidDistance;

            if (rawLongGap > effectiveAvoidDist)
                continue;

            // 橫向間距檢測（edge-to-edge，扣除雙方半車寬）
            float lateralDiff = agent.CurrentOffset - other.CurrentOffset;
            float lateralAbs = Mathf.Abs(lateralDiff);
            float combinedHalfWidth = (agent.BikeWidth + other.BikeWidth) * 0.5f;
            float effectiveSeparation = combinedHalfWidth + lateralMinSeparation;

            if (lateralAbs >= effectiveSeparation)
                continue;

            // 計算推開量：間距越小推力越大，縱向越近推力越大
            float lateralRatio = 1f - (lateralAbs / effectiveSeparation);
            float longitudinalRatio = 1f - (rawLongGap / effectiveAvoidDist);
            float pushAmount = lateralRatio * longitudinalRatio * lateralAvoidStrength;

            // 推開方向：往遠離對方的方向推（幾乎重疊時隨機挑一邊）
            if (lateralAbs < 0.01f)
                totalPush += (Random.value > 0.5f ? 1f : -1f) * pushAmount;
            else
                totalPush += Mathf.Sign(lateralDiff) * pushAmount;
        }

        desiredOffset += totalPush;
    }

    /// <summary>
    /// 根據 BikeAITrafficManager 的比賽階段更新運行時倍率。
    /// 開賽衝刺：超車更積極、追趕較弱（讓集團自然形成）。
    /// 尾段衝刺：超車更積極、追趕更強（製造精采的最後幾圈）。
    /// 中段 / 未啟用：所有倍率 = 1。
    /// </summary>
    private void UpdatePhaseMultipliers()
    {
        phaseOvertakeDistanceMul = 1f;
        phaseCatchUpMul = 1f;
        phaseOvertakeSpeedMul = 1f;

        if (!enableRacePhase)
            return;

        BikeAITrafficManager traffic = BikeAITrafficManager.Instance;
        if (traffic == null)
            return;

        switch (traffic.CurrentPhase)
        {
            case BikeAITrafficManager.RacePhase.EarlyRace:
                phaseOvertakeDistanceMul = earlyOvertakeDistanceMultiplier;
                phaseCatchUpMul = earlyCatchUpMultiplier;
                break;

            case BikeAITrafficManager.RacePhase.LateRace:
                phaseOvertakeDistanceMul = lateOvertakeDistanceMultiplier;
                phaseCatchUpMul = lateCatchUpMultiplier;
                phaseOvertakeSpeedMul = lateOvertakeSpeedMultiplier;
                break;

            // MidRace / PreRace / Finished：使用預設值 1
        }
    }
}