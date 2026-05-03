using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PhotoFreeCam : MonoBehaviour
{
    public TrackPhotoRig rig;
    public PhotoGallery gallery;
    public bool inputEnabled = true;

    [Header("Look")]
    public float mouseSensitivity = 1.6f;
    public float lookSmooth = 12f;
    public bool lockCursor = true;

    [Header("Move")]
    public float moveSpeed = 4f;
    public float sprintMul = 2f;
    public float slowMul = 0.3f;

    [Header("Ground Mode")]
    public float gravity = -15f;
    public float jumpForce = 5f;
    public float eyeHeight = 1.6f;
    public float stepOffset = 0.3f;

    [Header("Fly Mode（無 CharacterController 時使用）")]
    public float flySpeed = 8f;
    public float accel = 20f;
    public float decel = 20f;

    [Header("Focal Length")]
    public float focalMin = 18f;
    public float focalMax = 200f;
    public float focalStep = 2f;

    [Header("Rig Integration")]
    public float panAssistResumeDelay = 0.35f;

    Camera cam;
    CharacterController cc;
    Vector3 vMove;
    float yaw;
    float pitch;
    float panAssistResumeAt = -1f;
    float verticalVelocity;
    bool grounded;

    public bool IsGroundMode => cc != null;

    void Reset()
    {
        cam = GetComponent<Camera>();
        rig = GetComponent<TrackPhotoRig>();
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        if (!rig) rig = GetComponent<TrackPhotoRig>();
        cc = GetComponent<CharacterController>();
        if (cc == null) cc = GetComponentInParent<CharacterController>();

        if (cc != null)
        {
            cc.stepOffset = stepOffset;
            cc.skinWidth = 0.05f;
        }

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
        if (gallery != null && gallery.IsOpen) return;

        HandleLook();

        if (IsGroundMode)
            HandleGroundMove();
        else
            HandleFlyMove();

        TryResumePanAssist();
    }

    // 取得 CharacterController 所在的 Transform（地面模式的移動對象）
    Transform MoveRoot => cc != null ? cc.transform : transform;

    void HandleLook()
    {
        float mx = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
        float my = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        if (Mathf.Abs(mx) > 0.0001f || Mathf.Abs(my) > 0.0001f) PausePanAssist();

        yaw += mx;
        pitch = Mathf.Clamp(pitch - my, -89.5f, 89.5f);

        // 地面模式：根物件只轉 yaw，相機自己轉 pitch
        if (IsGroundMode)
        {
            MoveRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        else
        {
            var target = Quaternion.Euler(pitch, yaw, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-lookSmooth * Time.deltaTime));
        }
    }

    void HandleGroundMove()
    {
        // 水平移動方向（基於 yaw，不含 pitch）
        Quaternion flatRot = Quaternion.Euler(0f, yaw, 0f);
        Vector3 forward = flatRot * Vector3.forward;
        Vector3 right = flatRot * Vector3.right;

        Vector3 wish = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) wish += forward;
        if (Input.GetKey(KeyCode.S)) wish -= forward;
        if (Input.GetKey(KeyCode.A)) wish -= right;
        if (Input.GetKey(KeyCode.D)) wish += right;

        float mul = 1f;
        if (Input.GetKey(KeyCode.LeftShift)) mul *= sprintMul;
        if (Input.GetKey(KeyCode.LeftControl)) mul *= slowMul;

        Vector3 horizontal = wish.normalized * moveSpeed * mul;

        // 重力與跳躍
        grounded = cc.isGrounded;
        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (grounded && Input.GetKeyDown(KeyCode.Space))
            verticalVelocity = jumpForce;

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 move = horizontal + Vector3.up * verticalVelocity;
        cc.Move(move * Time.deltaTime);

        if (horizontal.sqrMagnitude > 0.0001f) PausePanAssist();
    }

    void HandleFlyMove()
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

        Vector3 target = wish.normalized * flySpeed * mul;

        if (target.sqrMagnitude > vMove.sqrMagnitude)
            vMove = Vector3.MoveTowards(vMove, target, accel * Time.deltaTime);
        else
            vMove = Vector3.MoveTowards(vMove, target, decel * Time.deltaTime);

        if (vMove.sqrMagnitude > 0.000001f) PausePanAssist();

        transform.position += vMove * Time.deltaTime;
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
