using UnityEngine;

public class PhotoScoreEvaluator : MonoBehaviour
{
    public TrackPhotoRig rig;

    [Header("權重")]
    [Range(0f, 1f)] public float compositionWeight = 0.30f;
    [Range(0f, 1f)] public float exposureWeight = 0.25f;
    [Range(0f, 1f)] public float sharpnessWeight = 0.25f;
    [Range(0f, 1f)] public float focusWeight = 0.20f;

    [System.Serializable]
    public class PhotoScore
    {
        public float composition;
        public float exposure;
        public float sharpness;
        public float focus;
        public float total;

        public float shutterSpeed;
        public float aperture;
        public float focalLength;
        public int iso;
        public float focusDistance;
        public string timestamp;
    }

    // 評分時暫存的最佳主體
    Transform evaluatedSubject;

    void Reset()
    {
        if (!rig) rig = GetComponent<TrackPhotoRig>();
    }

    public PhotoScore Evaluate()
    {
        if (rig == null || rig.cam == null) return null;

        var score = new PhotoScore
        {
            shutterSpeed = rig.shutterSpeedSec,
            aperture = rig.apertureF,
            focalLength = rig.focalLengthMm,
            iso = rig.GetISO(),
            focusDistance = rig.focusDistanceM,
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        // 從場上所有車輛中找出畫面中最佳主體
        evaluatedSubject = FindBestSubjectInFrame();

        if (evaluatedSubject == null)
        {
            score.total = 0f;
            return score;
        }

        score.composition = EvalComposition();
        score.exposure = EvalExposure();
        score.sharpness = EvalSharpness();
        score.focus = EvalFocus();

        float wSum = compositionWeight + exposureWeight + sharpnessWeight + focusWeight;
        if (wSum < 0.001f) wSum = 1f;
        score.total = (score.composition * compositionWeight
                     + score.exposure * exposureWeight
                     + score.sharpness * sharpnessWeight
                     + score.focus * focusWeight) / wSum;

        return score;
    }

    /// <summary>從場上所有車輛中找出在畫面內且最靠近中心的主體</summary>
    Transform FindBestSubjectInFrame()
    {
        var mgr = BikeAITrafficManager.Instance;
        if (mgr == null) return null;

        var agents = mgr.RankedAgents;
        if (agents == null || agents.Count == 0) return null;

        Camera cam = rig.cam;
        Transform best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < agents.Count; i++)
        {
            Transform t = agents[i].transform;
            Vector3 vp = cam.WorldToViewportPoint(t.position);

            // 必須在相機前方且在畫面內
            if (vp.z <= 0f) continue;
            if (vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue;

            // 以距畫面中心的距離作為評比（越近越好）
            float dist = Vector2.Distance(new Vector2(vp.x, vp.y), new Vector2(0.5f, 0.5f));
            if (dist < bestScore)
            {
                bestScore = dist;
                best = t;
            }
        }

        return best;
    }

    float EvalComposition()
    {
        Vector3 sp = rig.cam.WorldToViewportPoint(evaluatedSubject.position);

        float sx = Mathf.Clamp01(sp.x);
        float sy = Mathf.Clamp01(sp.y);

        // 三分法交點
        Vector2[] thirds = {
            new Vector2(1f/3f, 1f/3f), new Vector2(2f/3f, 1f/3f),
            new Vector2(1f/3f, 2f/3f), new Vector2(2f/3f, 2f/3f)
        };

        float bestDist = float.MaxValue;
        foreach (var t in thirds)
        {
            float d = Vector2.Distance(new Vector2(sx, sy), t);
            if (d < bestDist) bestDist = d;
        }

        // 距離三分點越近分數越高
        float thirdsScore = Mathf.Lerp(100f, 60f, Mathf.Clamp01(bestDist / 0.15f));

        // 太靠邊緣扣分
        float edgePenalty = 0f;
        float margin = 0.08f;
        if (sx < margin || sx > 1f - margin) edgePenalty += 20f;
        if (sy < margin || sy > 1f - margin) edgePenalty += 20f;

        return Mathf.Clamp(thirdsScore - edgePenalty, 0f, 100f);
    }

    float EvalExposure()
    {
        int iso = rig.GetISO();
        float isoPenalty = 0f;
        if (iso > 3200) isoPenalty = 30f;
        else if (iso > 1600) isoPenalty = 15f;
        else if (iso > 800) isoPenalty = 5f;

        float compPenalty = Mathf.Abs(rig.exposureCompEV) * 10f;

        return Mathf.Clamp(100f - isoPenalty - compPenalty, 0f, 100f);
    }

    float EvalSharpness()
    {
        float subDist = Vector3.Distance(rig.cam.transform.position, evaluatedSubject.position);
        if (subDist <= 0f) return 50f;

        // 快門速度 vs 焦距安全快門規則（1/焦距 mm）
        float safeShutter = 1f / Mathf.Max(rig.focalLengthMm, 1f);
        float shutterRatio = rig.shutterSpeedSec / safeShutter;

        float score = 100f;
        if (shutterRatio > 4f) score -= 40f;
        else if (shutterRatio > 2f) score -= 20f;
        else if (shutterRatio > 1f) score -= 5f;

        // 搖攝補償：如果正在追蹤主體，快門慢也可以銳利
        if (rig.IsPanAssistActive && rig.subject != null)
            score += 15f;

        return Mathf.Clamp(score, 0f, 100f);
    }

    float EvalFocus()
    {
        float subDist = Vector3.Distance(rig.cam.transform.position, evaluatedSubject.position);
        if (subDist <= 0f) return 50f;

        float diff = Mathf.Abs(rig.focusDistanceM - subDist);
        float ratio = diff / Mathf.Max(subDist, 0.1f);

        if (ratio < 0.02f) return 100f;
        if (ratio < 0.05f) return 85f;
        if (ratio < 0.10f) return 60f;
        if (ratio < 0.20f) return 35f;
        return 10f;
    }
}
