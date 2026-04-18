using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MouseDragRigidbody : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Camera targetCamera;

    [Header("Drag Plane")]
    [SerializeField] private LayerMask draggableLayers = ~0;
    [SerializeField] private float dragPlaneDepth = 0f;

    private Rigidbody _draggedRigidbody;
    private Vector3 _grabPointLocal;
    private Vector3 _targetPosition;
    private Plane _dragPlane;
    private bool _isDragging;

    private void Awake()
    {
        RefreshConfiguration();
    }

    private void Update()
    {
        if (Mouse.current == null || targetCamera == null)
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryStartDrag();
        }

        if (_isDragging && Mouse.current.leftButton.isPressed)
        {
            UpdateDragTarget();
        }

        if (_isDragging && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            EndDrag();
        }
    }

    private void FixedUpdate()
    {
        if (!_isDragging || _draggedRigidbody == null)
            return;

        // El rigidbody se reposiciona a partir del punto exacto donde fue agarrado.
        _draggedRigidbody.MovePosition(_targetPosition);
    }

    private void TryStartDrag()
    {
        Ray ray = targetCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, draggableLayers))
            return;

        if (hit.rigidbody == null)
            return;

        _draggedRigidbody = hit.rigidbody;
        _grabPointLocal = _draggedRigidbody.transform.InverseTransformPoint(hit.point);
        _targetPosition = _draggedRigidbody.position;
        _isDragging = true;

        UpdateDragTarget();
    }

    private void UpdateDragTarget()
    {
        if (_draggedRigidbody == null)
            return;

        Ray ray = targetCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!_dragPlane.Raycast(ray, out float enter))
            return;

        // El cursor vive sobre un plano XY fijo y el rigidbody se ajusta para que
        // el mismo punto agarrado quede "colgando" del mouse.
        Vector3 desiredGrabPoint = ray.GetPoint(enter);
        Vector3 currentGrabPoint = _draggedRigidbody.transform.TransformPoint(_grabPointLocal);
        Vector3 deltaToTarget = desiredGrabPoint - currentGrabPoint;

        _targetPosition = _draggedRigidbody.position + deltaToTarget;
        _targetPosition.z = dragPlaneDepth;
    }

    private void EndDrag()
    {
        _isDragging = false;
        _draggedRigidbody = null;
    }

    private void OnValidate()
    {
        RefreshConfiguration();
    }

    private void RefreshConfiguration()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        // Plano XY: X horizontal, Y vertical, Z fijo.
        _dragPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, dragPlaneDepth));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_targetPosition, 0.15f);
    }
}
