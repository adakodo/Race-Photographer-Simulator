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

    float EvalComposition()
    {
        if (rig.subject == null) return 50f;

        Vector3 sp = rig.cam.WorldToViewportPoint(rig.subject.position);
        if (sp.z < 0f) return 0f;

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

        // 距離三分點越近分數越高（滿分距離 < 0.05，0 分距離 > 0.5）
        float thirdsScore = Mathf.Lerp(100f, 60f, Mathf.Clamp01(bestDist / 0.15f));

        // 太靠邊緣扣分
        float edgePenalty = 0f;
        float margin = 0.08f;
        if (sx < margin || sx > 1f - margin) edgePenalty += 20f;
        if (sy < margin || sy > 1f - margin) edgePenalty += 20f;

        // 主體不在畫面中
        if (sp.x < 0f || sp.x > 1f || sp.y < 0f || sp.y > 1f)
            return 0f;

        return Mathf.Clamp(thirdsScore - edgePenalty, 0f, 100f);
    }

    float EvalExposure()
    {
        // ISO 過高的懲罰
        int iso = rig.GetISO();
        float isoPenalty = 0f;
        if (iso > 3200) isoPenalty = 30f;
        else if (iso > 1600) isoPenalty = 15f;
        else if (iso > 800) isoPenalty = 5f;

        // 曝光補償偏離的懲罰
        float compPenalty = Mathf.Abs(rig.exposureCompEV) * 10f;

        return Mathf.Clamp(100f - isoPenalty - compPenalty, 0f, 100f);
    }

    float EvalSharpness()
    {
        if (rig.subject == null) return 70f;

        float subDist = rig.GetSubjectDistance();
        if (subDist <= 0f) return 50f;

        // 主體角速度（近似）
        Vector3 sp1 = rig.cam.WorldToViewportPoint(rig.subject.position);
        float focalM = rig.focalLengthMm * 0.001f;
        float angularSizeDeg = Mathf.Atan2(1f, subDist / focalM) * Mathf.Rad2Deg;

        // 快門速度 vs 焦距安全快門規則（1/焦距 mm）
        float safeShutter = 1f / Mathf.Max(rig.focalLengthMm, 1f);
        float shutterRatio = rig.shutterSpeedSec / safeShutter;

        // 快門越快越銳利
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
        if (rig.IsFocusConfirmed()) return 100f;

        float subDist = rig.GetSubjectDistance();
        if (subDist < 0f) return 50f;

        float diff = Mathf.Abs(rig.focusDistanceM - subDist);
        float ratio = diff / Mathf.Max(subDist, 0.1f);

        // 對焦誤差比例越小分數越高
        if (ratio < 0.02f) return 90f;
        if (ratio < 0.05f) return 70f;
        if (ratio < 0.10f) return 50f;
        if (ratio < 0.20f) return 30f;
        return 10f;
    }
}
