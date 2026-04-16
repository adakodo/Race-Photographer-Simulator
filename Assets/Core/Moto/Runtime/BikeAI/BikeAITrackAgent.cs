using UnityEngine;

[DisallowMultipleComponent]
public class BikeAITrackAgent : MonoBehaviour
{
    public enum OvertakeState
    {
        None,
        Preparing,
        Overtaking,
        Returning
    }

    [Header("References")]
    [SerializeField] private TrackSplineProvider track;
    [SerializeField] private BikeAIStrategy_Follow strategy;

    [Header("Speed")]
    [SerializeField] private float startSpeed = 18f;
    [SerializeField] private float maxSpeed = 40f;
    [SerializeField] private float acceleration = 18f;
    [SerializeField] private float brakeAcceleration = 35f;

    [Header("Lane")]
    [SerializeField] private float currentOffset = 0f;
    [SerializeField] private float laneOffsetSmooth = 4f;

    [Header("Rotation")]
    [SerializeField] private float rotationLerp = 10f;
    [SerializeField] private float leanSmooth = 8f;

    [Header("Lean — 曲率傾斜")]
    [Tooltip("最大傾斜角度（度）。MotoGP 約 50-62°")]
    [SerializeField] private float maxLeanAngle = 50f;
    [Tooltip("前方取樣距離（公尺），用於計算賽道曲率")]
    [SerializeField] private float curvatureLookAhead = 8f;
    [Tooltip("曲率角度達到此值時傾斜到最大角度")]
    [SerializeField] private float curvatureAngleForMaxLean = 30f;
    [Tooltip("換道偏移造成的額外傾斜比例（疊加在曲率傾斜上）")]
    [SerializeField] private float laneChangeLeanFactor = 15f;

    [Header("Dimensions — 車體尺寸")]
    [Tooltip("車身長度（公尺），用於縱向間距計算。MotoGP 約 2.0~2.2m")]
    [SerializeField] private float bikeLength = 2.1f;
    [Tooltip("車身寬度（公尺），用於橫向間距計算。MotoGP 約 0.6~0.7m")]
    [SerializeField] private float bikeWidth = 0.65f;

    [Header("Placement")]
    [SerializeField] private bool snapToNearestOnStart = true;
    [SerializeField] private float startDistanceOnTrack = 0f;
    [SerializeField] private float heightOffset = 0f;

    [Header("LOD")]
    [SerializeField] private float nearUpdateDistance = 40f;
    [SerializeField] private float farUpdateInterval = 0.1f;
    [SerializeField] private Transform lodReference;

    private float distanceOnTrack;
    private float currentSpeed;
    private float targetSpeed;
    private float targetOffset;
    private float currentLean;
    private float nextLogicTime;

    private OvertakeState overtakeState;
    private BikeAITrackAgent overtakeTarget;

    /// <summary>已完成的圈數（從 0 開始，通過起終點線時 +1）。</summary>
    private int currentLap;
    /// <summary>上一幀的 distanceOnTrack，用於偵測跨越起終點線。</summary>
    private float previousDistance;

    public TrackSplineProvider Track => track;
    public float DistanceOnTrack => distanceOnTrack;
    public float CurrentSpeed => currentSpeed;
    public float CurrentOffset => currentOffset;
    public float TargetSpeed => targetSpeed;
    public float TargetOffset => targetOffset;
    public float MaxSpeed => maxSpeed;
    public float Acceleration => acceleration;
    public float BrakeAcceleration => brakeAcceleration;
    public float BikeLength => bikeLength;
    public float BikeWidth => bikeWidth;
    public OvertakeState CurrentOvertakeState => overtakeState;
    public BikeAITrackAgent OvertakeTarget => overtakeTarget;

    /// <summary>已完成的圈數。</summary>
    public int CurrentLap => currentLap;
    /// <summary>總比賽距離（圈數 × 賽道長度 + 當前距離），用於名次排序。</summary>
    public float TotalRaceDistance => track != null && track.IsValid()
        ? currentLap * track.Length + distanceOnTrack
        : 0f;

    private void OnEnable()
    {
        if (BikeAITrafficManager.Instance != null)
            BikeAITrafficManager.Instance.Register(this);
    }

    private void OnDisable()
    {
        if (BikeAITrafficManager.Instance != null)
            BikeAITrafficManager.Instance.Unregister(this);
    }

    private void Start()
    {
        currentSpeed = Mathf.Clamp(startSpeed, 0f, maxSpeed);
        targetSpeed = currentSpeed;
        targetOffset = currentOffset;

        if (track == null || !track.IsValid())
            return;

        if (snapToNearestOnStart)
            distanceOnTrack = track.GetNearestDistance(transform.position);
        else
            distanceOnTrack = track.WrapDistance(startDistanceOnTrack);

        ApplyPoseImmediate();
    }

    private void Update()
    {
        if (track == null || !track.IsValid())
            return;

        float deltaTime = Time.deltaTime;

        if (ShouldRunLogicThisFrame())
        {
            if (strategy != null)
                strategy.Tick(this);

            nextLogicTime = Time.time + GetLogicInterval();
        }

        UpdateMovement(deltaTime);
        UpdateTransform(deltaTime);
    }

    /// <summary>由 Spawner 呼叫，在 Start 前注入賽道引用與起始位置。</summary>
    public void InitTrack(TrackSplineProvider trackProvider, float startDist, float startOffsetLateral = 0f)
    {
        track = trackProvider;
        startDistanceOnTrack = startDist;
        currentOffset = startOffsetLateral;
        snapToNearestOnStart = false;
    }

    public void SetTargetSpeed(float value)
    {
        targetSpeed = Mathf.Clamp(value, 0f, maxSpeed);
    }

    public void SetTargetOffset(float value)
    {
        float tf = track != null && track.IsValid() ? track.DistanceToTF(distanceOnTrack) : 0f;
        targetOffset = track != null ? track.ClampOffset(tf, value) : value;
    }

    public void SetOvertakeState(OvertakeState state, BikeAITrackAgent target)
    {
        overtakeState = state;
        overtakeTarget = target;
    }

    private bool ShouldRunLogicThisFrame()
    {
        return Time.time >= nextLogicTime;
    }

    private float GetLogicInterval()
    {
        Transform reference = lodReference != null ? lodReference : Camera.main != null ? Camera.main.transform : null;
        if (reference == null)
            return 0f;

        float sqrDistance = (reference.position - transform.position).sqrMagnitude;
        return sqrDistance <= nearUpdateDistance * nearUpdateDistance ? 0f : farUpdateInterval;
    }

    private void UpdateMovement(float deltaTime)
    {
        float accel = targetSpeed >= currentSpeed ? acceleration : brakeAcceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * deltaTime);
        currentOffset = Mathf.MoveTowards(currentOffset, targetOffset, laneOffsetSmooth * deltaTime);

        previousDistance = distanceOnTrack;
        distanceOnTrack += currentSpeed * deltaTime;

        // 圈數偵測：距離超過賽道長度時代表通過起終點線
        float trackLength = track.Length;
        if (trackLength > 0f && distanceOnTrack >= trackLength)
        {
            currentLap++;
        }

        distanceOnTrack = track.WrapDistance(distanceOnTrack);
    }

    private void UpdateTransform(float deltaTime)
    {
        TrackSplineProvider.TrackSample sample = track.GetSampleByDistance(distanceOnTrack);

        Vector3 finalPosition = sample.position + sample.right * currentOffset + sample.up * heightOffset;
        Quaternion targetRotation = Quaternion.LookRotation(sample.forward, sample.up);

        // --- 曲率傾斜：取前方樣本計算賽道彎曲方向 ---
        float aheadDist = distanceOnTrack + curvatureLookAhead;
        TrackSplineProvider.TrackSample aheadSample = track.GetSampleByDistance(aheadDist);

        // 使用 SignedAngle 取得帶方向的曲率角度（正值 = 左彎，負值 = 右彎）
        float curvatureAngle = Vector3.SignedAngle(sample.forward, aheadSample.forward, sample.up);

        // 將曲率角度映射到傾斜角度（彎越急傾越多）
        float curvatureRatio = Mathf.Clamp(curvatureAngle / curvatureAngleForMaxLean, -1f, 1f);
        float curvatureLean = -curvatureRatio * maxLeanAngle;

        // --- 換道傾斜：橫向偏移變化疊加額外傾斜 ---
        float offsetDelta = targetOffset - currentOffset;
        float laneChangeLean = Mathf.Clamp(-offsetDelta * laneChangeLeanFactor, -maxLeanAngle, maxLeanAngle);

        // --- 合併並限制最大傾斜角 ---
        float targetLean = Mathf.Clamp(curvatureLean + laneChangeLean, -maxLeanAngle, maxLeanAngle);
        currentLean = Mathf.MoveTowards(currentLean, targetLean, leanSmooth * 100f * deltaTime);

        Quaternion leanRotation = Quaternion.AngleAxis(currentLean, Vector3.forward);
        Quaternion finalRotation = targetRotation * leanRotation;

        transform.position = finalPosition;
        transform.rotation = Quaternion.Slerp(transform.rotation, finalRotation, rotationLerp * deltaTime);
    }

    private void ApplyPoseImmediate()
    {
        if (track == null || !track.IsValid())
            return;

        TrackSplineProvider.TrackSample sample = track.GetSampleByDistance(distanceOnTrack);
        transform.position = sample.position + sample.right * currentOffset + sample.up * heightOffset;
        transform.rotation = Quaternion.LookRotation(sample.forward, sample.up);
    }
}