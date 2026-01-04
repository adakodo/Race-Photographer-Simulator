using UnityEngine;
using UnityEngine.Rendering;

public class PhotoPrintSpawner : MonoBehaviour
{
    public PhotoCaptureManager capture;
    public Material photoMaterialTemplate;
    public Transform spawnPoint;
    public Vector2 worldSize = new Vector2(0.3f, 0.2f);
    public bool addRigidBody = false;

    void OnEnable()
    {
        if (capture != null) capture.OnPhotoReady += SpawnPhoto;
    }

    void OnDisable()
    {
        if (capture != null) capture.OnPhotoReady -= SpawnPhoto;
    }

    void SpawnPhoto(Texture2D tex)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "PhotoPrint";

        go.transform.position = spawnPoint ? spawnPoint.position : transform.position + transform.forward * 1.0f;
        go.transform.rotation = spawnPoint ? spawnPoint.rotation : Quaternion.LookRotation(transform.forward, Vector3.up);
        go.transform.localScale = new Vector3(worldSize.x, worldSize.y, 1f);

        var mc = go.GetComponent<MeshCollider>();
        if (mc) Destroy(mc);

        var mat = CreateMaterial();
        ApplyPhotoTexture(mat, tex);

        var mr = go.GetComponent<MeshRenderer>();
        mr.material = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;

        if (addRigidBody)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.AddForce(go.transform.forward * 1.5f + go.transform.up * 1.0f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 0.1f, ForceMode.Impulse);
        }

        float w = tex.width;
        float h = tex.height;
        Vector2 scale = new Vector2((w - 1f) / w, (h - 1f) / h);
        Vector2 offset = new Vector2(0.5f / w, 0.5f / h);

        if (mat.HasProperty("_BaseColorMap"))
        {
            mat.SetTextureScale("_BaseColorMap", scale);
            mat.SetTextureOffset("_BaseColorMap", offset);
        }
        else if (mat.HasProperty("_MainTex"))
        {
            mat.SetTextureScale("_MainTex", scale);
            mat.SetTextureOffset("_MainTex", offset);
        }
    }

    Material CreateMaterial()
    {
        if (photoMaterialTemplate != null)
            return new Material(photoMaterialTemplate);

        Shader sh = Shader.Find("HDRP/Unlit");
        if (sh == null) sh = Shader.Find("HDRP/Lit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        return new Material(sh);
    }

    void ApplyPhotoTexture(Material mat, Texture tex)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", Color.white);

        bool assigned = false;
        if (mat.HasProperty("_BaseColorMap")) { mat.SetTexture("_BaseColorMap", tex); assigned = true; }
        else if (mat.HasProperty("_UnlitColorMap")) { mat.SetTexture("_UnlitColorMap", tex); assigned = true; }
        else if (mat.HasProperty("_BaseMap")) { mat.SetTexture("_BaseMap", tex); assigned = true; }
        else if (mat.HasProperty("_MainTex")) { mat.SetTexture("_MainTex", tex); assigned = true; }

        if (assigned)
        {
            mat.EnableKeyword("_BASE_COLOR_MAP");
            mat.EnableKeyword("_BASEMAP");
            mat.EnableKeyword("_UNLIT_COLORMAP");
        }

        if (mat.HasProperty("_SurfaceType")) mat.SetFloat("_SurfaceType", 0f);
        if (mat.HasProperty("_AlphaCutoffEnable")) mat.SetFloat("_AlphaCutoffEnable", 0f);
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");

        if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", Color.white);
        if (mat.HasProperty("_EmissiveIntensity")) mat.SetFloat("_EmissiveIntensity", 1.0f);
        mat.EnableKeyword("_EMISSIVE_COLOR");
    }
}
