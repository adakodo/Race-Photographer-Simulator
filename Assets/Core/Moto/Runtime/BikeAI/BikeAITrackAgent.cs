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
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float brakeAcceleration = 20f;

    [Header("Lane")]
    [SerializeField] private float currentOffset = 0f;
    [SerializeField] private float laneOffsetSmooth = 4f;

    [Header("Rotation")]
    [SerializeField] private float rotationLerp = 10f;
    [SerializeField] private float leanAmount = 25f;
    [SerializeField] private float leanSmooth = 8f;

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

    public TrackSplineProvider Track => track;
    public float DistanceOnTrack => distanceOnTrack;
    public float CurrentSpeed => currentSpeed;
    public float CurrentOffset => currentOffset;
    public float TargetSpeed => targetSpeed;
    public float TargetOffset => targetOffset;
    public float MaxSpeed => maxSpeed;
    public float Acceleration => acceleration;
    public float BrakeAcceleration => brakeAcceleration;
    public OvertakeState CurrentOvertakeState => overtakeState;
    public BikeAITrackAgent OvertakeTarget => overtakeTarget;

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

        distanceOnTrack += currentSpeed * deltaTime;
        distanceOnTrack = track.WrapDistance(distanceOnTrack);
    }

    private void UpdateTransform(float deltaTime)
    {
        TrackSplineProvider.TrackSample sample = track.GetSampleByDistance(distanceOnTrack);

        Vector3 finalPosition = sample.position + sample.right * currentOffset + sample.up * heightOffset;
        Quaternion targetRotation = Quaternion.LookRotation(sample.forward, sample.up);

        float offsetDelta = targetOffset - currentOffset;
        float targetLean = Mathf.Clamp(-offsetDelta * leanAmount, -leanAmount, leanAmount);
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