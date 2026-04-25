using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PhotoFreeCam : MonoBehaviour
{
    public TrackPhotoRig rig;
    public bool inputEnabled = true;

    [Header("Look")]
    public float mouseSensitivity = 1.6f;
    public float lookSmooth = 12f;
    public bool lockCursor = true;

    [Header("Move")]
    public float moveSpeed = 8f;
    public float accel = 20f;
    public float decel = 20f;
    public float sprintMul = 3f;
    public float slowMul = 0.3f;

    [Header("Focal Length")]
    public float focalMin = 18f;
    public float focalMax = 200f;
    public float focalStep = 2f;

    [Header("Rig Integration")]
    public float panAssistResumeDelay = 0.35f;

    Camera cam;
    Vector3 vMove;
    float yaw;
    float pitch;
    float panAssistResumeAt = -1f;

    void Reset()
    {
        cam = GetComponent<Camera>();
        rig = GetComponent<TrackPhotoRig>();
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        if (!rig) rig = GetComponent<TrackPhotoRig>();

        var e = transform.rotation.eulerAngles;
        yaw = e.y;
        pitch = e.x;

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        if (!inputEnabled) { TryResumePanAssist(); return; }

        HandleLook();
        HandleMove();
        //HandleRigShortcuts();
        TryResumePanAssist();
    }

    void HandleLook()
    {
        float mx = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float my = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        if (Mathf.Abs(mx) > 0.0001f || Mathf.Abs(my) > 0.0001f) PausePanAssist();

        yaw += mx;
        pitch = Mathf.Clamp(pitch - my, -89.5f, 89.5f);

        var target = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-lookSmooth * Time.deltaTime));
    }

    void HandleMove()
    {
        Vector3 wish = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) wish += transform.forward;
        if (Input.GetKey(KeyCode.S)) wish -= transform.forward;
        if (Input.GetKey(KeyCode.A)) wish -= transform.right;
        if (Input.GetKey(KeyCode.D)) wish += transform.right;
        if (Input.GetKey(KeyCode.E)) wish += transform.up;
        if (Input.GetKey(KeyCode.Q)) wish -= transform.up;

        float mul = 1f;
        if (Input.GetKey(KeyCode.LeftShift)) mul *= sprintMul;
        if (Input.GetKey(KeyCode.LeftControl)) mul *= slowMul;

        Vector3 target = wish.normalized * moveSpeed * mul;

        if (target.sqrMagnitude > vMove.sqrMagnitude)
            vMove = Vector3.MoveTowards(vMove, target, accel * Time.deltaTime);
        else
            vMove = Vector3.MoveTowards(vMove, target, decel * Time.deltaTime);

        if (vMove.sqrMagnitude > 0.000001f) PausePanAssist();

        transform.position += vMove * Time.deltaTime;
    }

    void HandleRigShortcuts()
    {
        if (!rig) return;

        if (Input.GetMouseButtonDown(2))
        {
            Transform t = RaycastCenter();
            if (t) rig.SetSubject(t);
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (!rig.HoldShutter) rig.StartHoldShutter();
            else rig.StopHoldShutter();
        }
    }

    Transform RaycastCenter()
    {
        Ray r = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(r, out var hit, 10000f, ~0, QueryTriggerInteraction.Ignore))
            return hit.collider.transform;
        return null;
    }

    void PausePanAssist()
    {
        if (rig)
        {
            rig.PauseSystemPanAssist();
            panAssistResumeAt = Time.time + panAssistResumeDelay;
        }
    }

    void TryResumePanAssist()
    {
        if (!rig) return;
        if (Time.time >= panAssistResumeAt && vMove.sqrMagnitude < 0.00001f)
            rig.ResumeSystemPanAssist();
    }
}