using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraController2D_NewInput : MonoBehaviour
{
    [Header("Zoom")]
    public float zoomSpeed = 5f;
    public float minZoom = 2f;
    public float maxZoom = 20f;

    [Header("Keyboard Pan")]
    public float keyboardMoveSpeed = 10f;

    [Header("Drag Pan")]
    public float dragMoveSpeed = 1f; // tweak to taste

    private Camera cam;
    private Vector3 lastDragWorldPos;
    private bool isDragging;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true; // just to be safe
    }

    private void Update()
    {
        if (!Application.isFocused)
            return;

        HandleZoom();
        HandleKeyboardPan();
        HandleMouseDragPan();
    }

    private void HandleZoom()
    {
        if (Mouse.current == null)
            return;

        // Scroll is a Vector2: (x, y). We only care about y.
        float scrollY = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scrollY) > 0.01f)
        {
            // usually scrollY is ~120 or -120 per notch, so scale it down
            float zoomDelta = scrollY * 0.01f * zoomSpeed;

            cam.orthographicSize -= zoomDelta;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
        }
    }

    private void HandleKeyboardPan()
    {
        if (Keyboard.current == null)
            return;

        Vector2 input = Vector2.zero;

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            input.y += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            input.y -= 1f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            input.x -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            input.x += 1f;

        if (input.sqrMagnitude > 0f)
        {
            input = input.normalized;
            Vector3 move = new Vector3(input.x, input.y, 0f) * keyboardMoveSpeed * Time.deltaTime;
            transform.position += move;
        }
    }

    private void HandleMouseDragPan()
    {
        if (Mouse.current == null)
            return;

        // Middle mouse button pressed this frame → start drag
        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            isDragging = true;
            lastDragWorldPos = GetMouseWorldPosition();
        }

        // Middle mouse released → stop drag
        if (Mouse.current.middleButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        // While dragging, move camera by how much the mouse moved in world space
        if (isDragging && Mouse.current.middleButton.isPressed)
        {
            Vector3 currentWorldPos = GetMouseWorldPosition();
            Vector3 delta = lastDragWorldPos - currentWorldPos;

            // Optional scaling to make drag feel snappier/slower
            transform.position += new Vector3(delta.x, delta.y, 0f) * dragMoveSpeed;

            lastDragWorldPos = currentWorldPos;
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        var world = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -cam.transform.position.z));
        // For 2D we only care about x/y
        return new Vector3(world.x, world.y, 0f);
    }
}
