using FluffyUnderware.Curvy;
using UnityEngine;

[DisallowMultipleComponent]
public class TrackSplineProvider : MonoBehaviour
{
    [SerializeField] private CurvySpline spline;
    [SerializeField] private TrackWidthProfile widthProfile;
    [SerializeField] private bool refreshOnAwake = true;

    public CurvySpline Spline => spline;
    public TrackWidthProfile WidthProfile => widthProfile;

    public float Length
    {
        get
        {
            if (spline == null)
                return 0f;

            return spline.Length;
        }
    }

    private void Awake()
    {
        if (spline != null && refreshOnAwake && spline.Dirty)
        {
            spline.Refresh();
        }
    }

    public bool IsValid()
    {
        return spline != null && spline.IsInitialized && spline.Length > 0f;
    }

    public float WrapDistance(float distance)
    {
        if (!IsValid())
            return 0f;

        float length = spline.Length;
        if (length <= 0f)
            return 0f;

        distance %= length;
        if (distance < 0f)
            distance += length;

        return distance;
    }

    public float DistanceToTF(float distance)
    {
        if (!IsValid())
            return 0f;

        float wrapped = WrapDistance(distance);
        return spline.DistanceToTF(wrapped, CurvyClamping.Clamp);
    }

    public float TFToDistance(float tf)
    {
        if (!IsValid())
            return 0f;

        return spline.TFToDistance(tf, CurvyClamping.Clamp);
    }

    public float GetNearestDistance(Vector3 worldPosition)
    {
        if (!IsValid())
            return 0f;

        float tf = spline.GetNearestPointTF(worldPosition, Space.World);
        return spline.TFToDistance(tf, CurvyClamping.Clamp);
    }

    public float ClampOffset(float tf, float offset)
    {
        if (widthProfile == null)
            return offset;

        return widthProfile.ClampOffset(tf, offset);
    }

    public TrackSample GetSampleByDistance(float distance)
    {
        TrackSample sample = default;

        if (!IsValid())
            return sample;

        float wrappedDistance = WrapDistance(distance);
        float tf = spline.DistanceToTF(wrappedDistance, CurvyClamping.Clamp);

        Vector3 position = spline.InterpolateByDistanceFast(wrappedDistance, Space.World);
        Vector3 forward = spline.GetTangentByDistanceFast(wrappedDistance, Space.World).normalized;
        Vector3 up = spline.GetOrientationUpFast(tf, Space.World).normalized;

        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;

        if (up.sqrMagnitude < 0.0001f)
            up = Vector3.up;

        Vector3 right = Vector3.Cross(up, forward).normalized;
        if (right.sqrMagnitude < 0.0001f)
            right = transform.right;

        up = Vector3.Cross(forward, right).normalized;

        sample.position = position;
        sample.forward = forward;
        sample.up = up;
        sample.right = right;
        sample.tf = tf;
        sample.distance = wrappedDistance;

        return sample;
    }

    public struct TrackSample
    {
        public Vector3 position;
        public Vector3 forward;
        public Vector3 up;
        public Vector3 right;
        public float tf;
        public float distance;
    }
}