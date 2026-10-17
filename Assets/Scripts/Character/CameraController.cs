using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    [Header("Target & Positioning")]
    [SerializeField] private Transform target;
    [SerializeField] private float height = 0.45f;

    [Header("Touch & Drag Look Settings")]
    [Tooltip("Enable looking around by touching and dragging on the screen.")]
    [SerializeField] private bool enableTouchLook = true;

    [Tooltip("Speed multiplier for touch / drag camera rotation.")]
    [SerializeField] private float touchSensitivity = 0.15f;

    [Tooltip("Invert vertical look direction.")]
    [SerializeField] private bool invertY = false;

    [Tooltip("If true, touch camera rotation will only activate when touching the right side of the screen.")]
    [SerializeField] private bool requireRightHalfForTouch = false;

    [Header("Mouse Controls (Desktop / Legacy)")]
    [Tooltip("If true, moving the mouse cursor freely without clicking will rotate the camera (desktop FPS style). Leave false for mobile/touch mode.")]
    [SerializeField] private bool controlCameraWithMouse = false;

    [Tooltip("If true, keep the system cursor hidden.")]
    [SerializeField] private bool hideCursor = true;

    [Tooltip("Speed multiplier when controlCameraWithMouse is enabled.")]
    [SerializeField] private float sensitivity = 3.0f;

    [Header("Optional UI Zone")]
    [SerializeField] private TouchLookZone touchLookZone;

    private Transform cameraTransform;
    private Vector2 mouseLook;
    private Vector2 input;

    private int lookFingerId = -1;
    private Vector2 lastTouchPosition;
    private bool isMouseDraggingLook = false;
    private Vector2 lastMousePosition;

    void Awake()
    {
        cameraTransform = transform;
        if (touchLookZone == null)
        {
            touchLookZone = FindAnyObjectByType<TouchLookZone>();
        }
    }

    void Start()
    {
        UnlockCursor();

        if (target == null)
        {
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                target = player.transform;
            }
        }

        if (target != null)
        {
            mouseLook.x = target.eulerAngles.y;
            mouseLook.y = 0f;
        }
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = !hideCursor && !Application.isMobilePlatform;
    }

    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (GameController.IsPaused)
            return;

        if (target == null)
        {
            var player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                target = player.transform;
            }
            if (target == null) return;
        }

        Vector2 mouseInput = Vector2.zero;
        if (controlCameraWithMouse)
        {
            mouseInput = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * sensitivity;
        }

        Vector2 touchDelta = Vector2.zero;
        if (enableTouchLook)
        {
            touchDelta = ProcessTouchAndDragInput();
        }

        if (touchLookZone != null)
        {
            touchDelta += touchLookZone.ConsumeLookDelta();
        }

        input = mouseInput + touchDelta;

        mouseLook += input;
        mouseLook.y = Mathf.Clamp(mouseLook.y, -89f, 89f);

        Quaternion targetRotation = Quaternion.AngleAxis(mouseLook.x, Vector3.up);
        Quaternion cameraRotation = targetRotation * Quaternion.AngleAxis(-mouseLook.y, Vector3.right);

        target.rotation = targetRotation;
        cameraTransform.position = target.transform.position + Vector3.up * height;
        cameraTransform.rotation = cameraRotation;
    }

    private Vector2 ProcessTouchAndDragInput()
    {
        Vector2 deltaLook = Vector2.zero;

        // 1. Mobile Multi-Touch (iOS, Android, Unity Device Simulator)
        if (Input.touchCount > 0)
        {
            // Verify our active look finger is still valid
            if (lookFingerId != -1)
            {
                bool stillActive = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    if (Input.GetTouch(i).fingerId == lookFingerId)
                    {
                        stillActive = true;
                        break;
                    }
                }
                if (!stillActive)
                {
                    lookFingerId = -1;
                }
            }

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                if (touch.phase == TouchPhase.Began)
                {
                    if (lookFingerId == -1)
                    {
                        if (requireRightHalfForTouch && touch.position.x < Screen.width * 0.4f)
                        {
                            continue;
                        }

                        // Ignore if touch started on UI (buttons, joystick, etc.)
                        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                        {
                            continue;
                        }

                        lookFingerId = touch.fingerId;
                        lastTouchPosition = touch.position;
                    }
                }
                else if (touch.fingerId == lookFingerId)
                {
                    if (touch.phase == TouchPhase.Moved)
                    {
                        Vector2 curPos = touch.position;
                        Vector2 delta = curPos - lastTouchPosition;
                        lastTouchPosition = curPos;

                        deltaLook.x += delta.x;
                        deltaLook.y += invertY ? -delta.y : delta.y;
                    }
                    else if (touch.phase == TouchPhase.Stationary)
                    {
                        lastTouchPosition = touch.position;
                    }
                    else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        lookFingerId = -1;
                    }
                }
            }
        }
        else
        {
            // 2. Mouse Drag Fallback for Unity Editor testing / Desktop
            bool isDown = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            bool isHeld = Input.GetMouseButton(0) || Input.GetMouseButton(1);

            if (isDown)
            {
                // Only start dragging camera if cursor is NOT clicking a UI element
                if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
                {
                    isMouseDraggingLook = true;
                    lastMousePosition = Input.mousePosition;
                }
            }
            else if (isHeld && isMouseDraggingLook)
            {
                Vector2 curPos = (Vector2)Input.mousePosition;
                Vector2 delta = curPos - lastMousePosition;
                lastMousePosition = curPos;

                deltaLook.x += delta.x;
                deltaLook.y += invertY ? -delta.y : delta.y;
            }

            if (!isHeld)
            {
                isMouseDraggingLook = false;
            }
        }

        return deltaLook * touchSensitivity;
    }
}