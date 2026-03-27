using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class BikeAIDebugGizmos : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BikeAITrackAgent agent;

    [Header("Draw")]
    [SerializeField] private bool drawForward = true;
    [SerializeField] private bool drawOffset = true;
    [SerializeField] private bool drawFrontAgentLine = true;
    [SerializeField] private bool drawTrackSample = true;
    [SerializeField] private bool drawTextWhenSelected = true;

    [Header("Size")]
    [SerializeField] private float forwardLength = 2f;
    [SerializeField] private float offsetSphereRadius = 0.18f;
    [SerializeField] private float sampleSphereRadius = 0.12f;
    [SerializeField] private float textHeight = 2.2f;

    [Header("Color")]
    [SerializeField] private Color forwardColor = Color.cyan;
    [SerializeField] private Color offsetColor = Color.yellow;
    [SerializeField] private Color frontAgentLineColor = Color.red;
    [SerializeField] private Color sampleColor = Color.green;
    [SerializeField] private Color textColor = Color.white;

    private void Reset()
    {
        agent = GetComponent<BikeAITrackAgent>();
    }

    private void OnDrawGizmos()
    {
        DrawDebug(false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawDebug(true);
    }

    private void DrawDebug(bool selected)
    {
        if (agent == null)
            agent = GetComponent<BikeAITrackAgent>();

        if (agent == null || agent.Track == null || !agent.Track.IsValid())
            return;

        Vector3 origin = transform.position;
        TrackSplineProvider.TrackSample sample = agent.Track.GetSampleByDistance(agent.DistanceOnTrack);

        if (drawForward)
        {
            Gizmos.color = forwardColor;
            Gizmos.DrawLine(origin, origin + transform.forward * forwardLength);
        }

        if (drawTrackSample)
        {
            Gizmos.color = sampleColor;
            Gizmos.DrawSphere(sample.position, sampleSphereRadius);
            Gizmos.DrawLine(sample.position, sample.position + sample.forward * 1.5f);
            Gizmos.DrawLine(sample.position, sample.position + sample.right * 1.0f);
        }

        if (drawOffset)
        {
            Vector3 centerPos = sample.position;
            Vector3 offsetPos = sample.position + sample.right * agent.CurrentOffset;

            Gizmos.color = offsetColor;
            Gizmos.DrawLine(centerPos, offsetPos);
            Gizmos.DrawSphere(offsetPos, offsetSphereRadius);
        }

        BikeAITrackAgent frontAgent = null;
        float gap = -1f;

        if (BikeAITrafficManager.Instance != null)
        {
            frontAgent = BikeAITrafficManager.Instance.GetFrontAgent(agent, out gap);

            if (drawFrontAgentLine && frontAgent != null)
            {
                Gizmos.color = frontAgentLineColor;
                Gizmos.DrawLine(origin, frontAgent.transform.position);
            }
        }

#if UNITY_EDITOR
        if (drawTextWhenSelected && selected)
        {
            string frontName = frontAgent != null ? frontAgent.name : "None";

            string text =
                $"Name: {agent.name}\n" +
                $"Speed: {agent.CurrentSpeed:F2}\n" +
                $"TargetSpeed: {agent.TargetSpeed:F2}\n" +
                $"Distance: {agent.DistanceOnTrack:F2}\n" +
                $"Offset: {agent.CurrentOffset:F2}\n" +
                $"TargetOffset: {agent.TargetOffset:F2}\n" +
                $"OvertakeState: {agent.CurrentOvertakeState}\n" +
                $"Front: {frontName}\n" +
                $"FrontGap: {(gap >= 0f ? gap.ToString("F2") : "N/A")}";

            GUIStyle style = new GUIStyle(EditorStyles.whiteLabel);
            style.normal.textColor = textColor;
            style.fontSize = 11;

            Handles.Label(origin + Vector3.up * textHeight, text, style);
        }
#endif
    }
}