using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BikeAITrafficManager : MonoBehaviour
{
    public static BikeAITrafficManager Instance { get; private set; }

    private readonly List<BikeAITrackAgent> agents = new List<BikeAITrackAgent>();
    private readonly List<BikeAITrackAgent> sortedAgents = new List<BikeAITrackAgent>();

    public IReadOnlyList<BikeAITrackAgent> Agents => agents;
    public IReadOnlyList<BikeAITrackAgent> SortedAgents => sortedAgents;

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
        float bestGap = float.MaxValue;

        for (int i = 0; i < sortedAgents.Count; i++)
        {
            BikeAITrackAgent other = sortedAgents[i];
            if (other == null || other == current || other.Track != track)
                continue;

            float gap = other.DistanceOnTrack - current.DistanceOnTrack;
            if (gap <= 0f)
                gap += trackLength;

            if (gap < bestGap)
            {
                bestGap = gap;
                best = other;
            }
        }

        forwardGap = bestGap;
        return best;
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

            float gap = Mathf.Abs(other.DistanceOnTrack - current.DistanceOnTrack);
            gap = Mathf.Min(gap, trackLength - gap);

            if (gap > checkGap)
                continue;

            if (Mathf.Abs(other.CurrentOffset - targetOffset) <= offsetTolerance)
                return true;
        }

        return false;
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
}