using UnityEngine;

[DisallowMultipleComponent]
public class BikeAIStrategy_Follow : MonoBehaviour
{
    [Header("Base")]
    [SerializeField] private float cruiseSpeed = 28f;
    [SerializeField] private float defaultOffset = 0f;

    [Header("Follow")]
    [SerializeField] private float followDistance = 10f;
    [SerializeField] private float brakeDistance = 6f;
    [SerializeField] private float minFollowSpeed = 4f;

    [Header("Curve")]
    [SerializeField] private float curveLookAhead = 12f;
    [SerializeField] private float curveAngleForFullSlowdown = 50f;
    [SerializeField] private float minCurveSpeedRatio = 0.55f;

    [Header("Overtake")]
    [SerializeField] private bool enableOvertake = true;
    [SerializeField] private float overtakeOffset = 1.5f;
    [SerializeField] private float overtakeTriggerDistance = 8f;
    [SerializeField] private float overtakeCompleteDistance = 2.5f;
    [SerializeField] private float returnOffsetThreshold = 0.15f;
    [SerializeField] private float laneCheckGap = 8f;
    [SerializeField] private float laneOffsetTolerance = 0.8f;
    [SerializeField] private float overtakeSpeedBonus = 3f;

    public void Tick(BikeAITrackAgent agent)
    {
        if (agent == null || agent.Track == null || !agent.Track.IsValid())
            return;

        float desiredSpeed = Mathf.Min(cruiseSpeed, agent.MaxSpeed);
        float desiredOffset = defaultOffset;

        ApplyCurveSlowdown(agent, ref desiredSpeed);
        ApplyTrafficLogic(agent, ref desiredSpeed, ref desiredOffset);

        agent.SetTargetSpeed(desiredSpeed);
        agent.SetTargetOffset(desiredOffset);
    }

    private void ApplyCurveSlowdown(BikeAITrackAgent agent, ref float desiredSpeed)
    {
        TrackSplineProvider track = agent.Track;

        float d0 = agent.DistanceOnTrack;
        float d1 = track.WrapDistance(d0 + curveLookAhead * 0.5f);
        float d2 = track.WrapDistance(d0 + curveLookAhead);

        Vector3 f0 = track.GetSampleByDistance(d0).forward;
        Vector3 f1 = track.GetSampleByDistance(d1).forward;
        Vector3 f2 = track.GetSampleByDistance(d2).forward;

        float angleA = Vector3.Angle(f0, f1);
        float angleB = Vector3.Angle(f1, f2);
        float curveAngle = Mathf.Max(angleA, angleB);

        float t = Mathf.InverseLerp(0f, curveAngleForFullSlowdown, curveAngle);
        float speedRatio = Mathf.Lerp(1f, minCurveSpeedRatio, t);

        desiredSpeed *= speedRatio;
    }

    private void ApplyTrafficLogic(BikeAITrackAgent agent, ref float desiredSpeed, ref float desiredOffset)
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
            desiredOffset = defaultOffset;
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
            desiredOffset = defaultOffset;
            return;
        }

        switch (agent.CurrentOvertakeState)
        {
            case BikeAITrackAgent.OvertakeState.None:
                {
                    if (gap < overtakeTriggerDistance && frontAgent.CurrentSpeed < desiredSpeed)
                    {
                        float leftOffset = defaultOffset - overtakeOffset;
                        float rightOffset = defaultOffset + overtakeOffset;

                        bool leftBlocked = traffic.IsOffsetBlocked(agent, leftOffset, laneCheckGap, laneOffsetTolerance);
                        bool rightBlocked = traffic.IsOffsetBlocked(agent, rightOffset, laneCheckGap, laneOffsetTolerance);

                        if (!leftBlocked)
                        {
                            desiredOffset = leftOffset;
                            desiredSpeed = Mathf.Min(agent.MaxSpeed, desiredSpeed + overtakeSpeedBonus);
                            agent.SetOvertakeState(BikeAITrackAgent.OvertakeState.Preparing, frontAgent);
                        }
                        else if (!rightBlocked)
                        {
                            desiredOffset = rightOffset;
                            desiredSpeed = Mathf.Min(agent.MaxSpeed, desiredSpeed + overtakeSpeedBonus);
                            agent.SetOvertakeState(BikeAITrackAgent.OvertakeState.Preparing, frontAgent);
                        }
                        else
                        {
                            desiredOffset = defaultOffset;
                        }
                    }
                    else
                    {
                        desiredOffset = defaultOffset;
                    }

                    break;
                }

            case BikeAITrackAgent.OvertakeState.Preparing:
                {
                    desiredSpeed = Mathf.Min(agent.MaxSpeed, desiredSpeed + overtakeSpeedBonus);
                    desiredOffset = agent.TargetOffset;

                    if (Mathf.Abs(agent.CurrentOffset - agent.TargetOffset) < 0.2f)
                    {
                        agent.SetOvertakeState(BikeAITrackAgent.OvertakeState.Overtaking, agent.OvertakeTarget);
                    }

                    break;
                }

            case BikeAITrackAgent.OvertakeState.Overtaking:
                {
                    desiredSpeed = Mathf.Min(agent.MaxSpeed, desiredSpeed + overtakeSpeedBonus);
                    desiredOffset = agent.TargetOffset;

                    BikeAITrackAgent target = agent.OvertakeTarget;
                    if (target == null)
                    {
                        agent.SetOvertakeState(BikeAITrackAgent.OvertakeState.Returning, null);
                    }
                    else
                    {
                        float passGap = agent.DistanceOnTrack - target.DistanceOnTrack;
                        if (passGap < 0f)
                            passGap += agent.Track.Length;

                        if (passGap > overtakeCompleteDistance && passGap < agent.Track.Length * 0.5f)
                        {
                            agent.SetOvertakeState(BikeAITrackAgent.OvertakeState.Returning, null);
                        }
                    }

                    break;
                }

            case BikeAITrackAgent.OvertakeState.Returning:
                {
                    desiredOffset = defaultOffset;

                    if (Mathf.Abs(agent.CurrentOffset - defaultOffset) <= returnOffsetThreshold)
                    {
                        agent.SetOvertakeState(BikeAITrackAgent.OvertakeState.None, null);
                    }

                    break;
                }
        }
    }
}