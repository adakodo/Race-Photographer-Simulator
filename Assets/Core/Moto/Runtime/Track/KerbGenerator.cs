using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 沿著賽道彎道內側自動生成紅白相間路緣石（Kerb）Mesh。
/// 透過 TrackSplineProvider 取樣曲率偵測彎道，在內側邊緣生成帶條紋的 Mesh。
/// </summary>
[ExecuteInEditMode]
public class KerbGenerator : MonoBehaviour
{
    [Header("賽道引用")]
    [SerializeField] private TrackSplineProvider track;

    [Header("曲率偵測")]
    [Tooltip("取樣間距（公尺）")]
    [SerializeField] private float sampleInterval = 1f;
    [Tooltip("曲率閾值（度/公尺），超過此值視為彎道")]
    [SerializeField] private float curvatureThreshold = 1.5f;
    [Tooltip("曲率計算前視距離（公尺）")]
    [SerializeField] private float curvatureLookAhead = 5f;
    [Tooltip("最短彎道長度（公尺），短於此不生成")]
    [SerializeField] private float minCurveLength = 5f;
    [Tooltip("彎道前後延伸距離（公尺）")]
    [SerializeField] private float curveExtension = 2f;

    [Header("Kerb 幾何")]
    [Tooltip("從 WidthProfile 邊界再往外推的額外距離（公尺），用於對齊實際路面邊緣")]
    [SerializeField] private float edgeOffset = 2f;
    [Tooltip("路緣石寬度（公尺）")]
    [SerializeField] private float kerbWidth = 1.2f;
    [Tooltip("路緣石高度（公尺）")]
    [SerializeField] private float kerbHeight = 0.03f;
    [Tooltip("Mesh 取樣間距（公尺）")]
    [SerializeField] private float meshInterval = 0.5f;

    [Header("外觀")]
    [Tooltip("每條紋沿路緣石方向的長度（公尺）")]
    [SerializeField] private float stripeLength = 0.5f;
    [SerializeField] private Color colorA = new Color(0.85f, 0.12f, 0.1f);
    [SerializeField] private Color colorB = Color.white;

    [Header("貼地 Raycast")]
    [SerializeField] private bool snapToGround = true;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float rayUpOffset = 3f;
    [SerializeField] private float rayMaxDistance = 10f;

    struct CurveSection
    {
        public float startDistance;
        public float endDistance;
        /// <summary>+1 = 右彎（kerb 在右），-1 = 左彎（kerb 在左）。</summary>
        public float sign;
    }

    [ContextMenu("生成路緣石")]
    public void Generate()
    {
        Clear();

        if (track == null || !track.IsValid())
        {
            Debug.LogWarning("[KerbGenerator] TrackSplineProvider 無效");
            return;
        }

        List<CurveSection> sections = DetectCurveSections();
        if (sections.Count == 0)
        {
            Debug.Log("[KerbGenerator] 未偵測到符合條件的彎道");
            return;
        }

        Material mat = CreateKerbMaterial();

        for (int i = 0; i < sections.Count; i++)
        {
            GenerateKerbMesh(sections[i], i, mat);
        }

        Debug.Log($"[KerbGenerator] 已生成 {sections.Count} 段路緣石");
    }

    [ContextMenu("清除路緣石")]
    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name.StartsWith("Kerb_"))
            {
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }
    }

    /// <summary>沿 Spline 取樣曲率，找出所有超過閾值的連續彎道區段。</summary>
    List<CurveSection> DetectCurveSections()
    {
        List<CurveSection> sections = new List<CurveSection>();
        float totalLength = track.Length;
        int sampleCount = Mathf.CeilToInt(totalLength / sampleInterval);

        bool inCurve = false;
        float curveStart = 0f;
        float signSum = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float d = i * sampleInterval;
            var s0 = track.GetSampleByDistance(d);
            var s1 = track.GetSampleByDistance(d + curvatureLookAhead);
            float signedAngle = Vector3.SignedAngle(s0.forward, s1.forward, s0.up);
            float curvature = Mathf.Abs(signedAngle / curvatureLookAhead);

            if (!inCurve && curvature >= curvatureThreshold)
            {
                inCurve = true;
                curveStart = d;
                signSum = signedAngle;
            }
            else if (inCurve)
            {
                if (curvature >= curvatureThreshold)
                {
                    signSum += signedAngle;
                }
                else
                {
                    TryAddSection(sections, curveStart, d, signSum);
                    inCurve = false;
                    signSum = 0f;
                }
            }
        }

        if (inCurve)
            TryAddSection(sections, curveStart, sampleCount * sampleInterval, signSum);

        return sections;
    }

    void TryAddSection(List<CurveSection> sections, float start, float end, float signSum)
    {
        float curveLen = end - start;
        if (curveLen < minCurveLength)
            return;

        // 正角 = 左彎 → 內側在左（-right）→ sign = -1
        // 負角 = 右彎 → 內側在右（+right）→ sign = +1
        // 注意：實際方向依賽道 Spline 座標系而定，若生成在外側則反轉
        float sign = signSum > 0 ? 1f : -1f;

        sections.Add(new CurveSection
        {
            startDistance = track.WrapDistance(start - curveExtension),
            endDistance = track.WrapDistance(end + curveExtension),
            sign = sign
        });
    }

    void GenerateKerbMesh(CurveSection section, int index, Material mat)
    {
        float sectionLength = section.endDistance - section.startDistance;
        if (sectionLength < 0f)
            sectionLength += track.Length;

        int stepCount = Mathf.CeilToInt(sectionLength / meshInterval) + 1;
        if (stepCount < 2) return;

        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        float vAccum = 0f;

        for (int i = 0; i < stepCount; i++)
        {
            float d = track.WrapDistance(section.startDistance + i * meshInterval);
            var sample = track.GetSampleByDistance(d);

            // 內側邊緣位置（WidthProfile 寬度 + 額外偏移，對齊實際路面邊緣）
            float edgeWidth = GetInsideWidth(sample.tf, section.sign) + edgeOffset;
            Vector3 edgeDir = sample.right * section.sign;
            Vector3 innerPos = sample.position + edgeDir * edgeWidth;
            Vector3 outerPos = sample.position + edgeDir * (edgeWidth + kerbWidth);

            // 貼地 + 微幅抬高
            innerPos = SnapPosition(innerPos, sample.up);
            outerPos = SnapPosition(outerPos, sample.up);

            // 轉為本地座標
            innerPos = transform.InverseTransformPoint(innerPos);
            outerPos = transform.InverseTransformPoint(outerPos);

            float v = vAccum / stripeLength;
            int vi = vertices.Count;

            vertices.Add(innerPos);
            vertices.Add(outerPos);
            uvs.Add(new Vector2(0f, v));
            uvs.Add(new Vector2(1f, v));

            if (i > 0)
            {
                int prev = vi - 2;
                triangles.Add(prev);
                triangles.Add(prev + 1);
                triangles.Add(vi + 1);

                triangles.Add(prev);
                triangles.Add(vi + 1);
                triangles.Add(vi);
            }

            vAccum += meshInterval;
        }

        Mesh mesh = new Mesh();
        mesh.name = $"Kerb_{index}";
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject go = new GameObject($"Kerb_{index}");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
    }

    float GetInsideWidth(float tf, float sign)
    {
        if (track.WidthProfile == null)
            return 2f;

        return sign > 0f
            ? track.WidthProfile.GetRightWidth(tf)
            : track.WidthProfile.GetLeftWidth(tf);
    }

    Vector3 SnapPosition(Vector3 pos, Vector3 up)
    {
        if (!snapToGround)
            return pos + up * kerbHeight;

        Vector3 origin = pos + up * rayUpOffset;
        if (Physics.Raycast(origin, -up, out RaycastHit hit, rayUpOffset + rayMaxDistance,
                            groundMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point + up * kerbHeight;
        }

        return pos + up * kerbHeight;
    }

    Material CreateKerbMaterial()
    {
        // 生成紅白條紋 Texture（2 像素高，每像素代表一條紋）
        int texW = 4;
        int texH = 2;
        Texture2D tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.hideFlags = HideFlags.HideAndDontSave;

        for (int x = 0; x < texW; x++)
        {
            tex.SetPixel(x, 0, colorA);
            tex.SetPixel(x, 1, colorB);
        }
        tex.Apply();

        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null)
        {
            Debug.LogWarning("[KerbGenerator] 找不到 HDRP/Lit shader，改用 Standard");
            shader = Shader.Find("Standard");
        }

        Material mat = new Material(shader);
        mat.name = "Kerb_Stripe";
        mat.hideFlags = HideFlags.HideAndDontSave;

        // HDRP/Lit 的 base color map 屬性
        if (mat.HasProperty("_BaseColorMap"))
            mat.SetTexture("_BaseColorMap", tex);
        else
            mat.mainTexture = tex;

        return mat;
    }
}
