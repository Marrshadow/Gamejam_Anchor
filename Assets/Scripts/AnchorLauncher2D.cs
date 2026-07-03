using UnityEngine;

public class AnchorLauncher2D : MonoBehaviour
{
    [Header("Anchor")]
    [SerializeField] private AnchorFreezeZone anchor;
    [SerializeField] private float flightSpeed = 18f;
    [SerializeField] private float anchorRadius = 0.18f;
    [SerializeField] private float freezeRadius = 2.4f;
    [SerializeField] private LayerMask freezableMask = ~0;

    private Vector3 targetPosition;
    private Vector2 mouseBoundScreenPosition;
    private bool isFlying;
    private bool isAnchored;
    // 是否绑定鼠标
    private bool isMouseBound = true;

    public bool IsMouseBound => isMouseBound;

    private void Awake()
    {
        if (anchor == null)
        {
            return;
        }

        anchor.Configure(anchorRadius, freezeRadius, freezableMask, transform);
        // ResetAnchorToOwner();
    }

    private void Update()
    {
        if (anchor == null)
        {
            return;
        }

        if (isMouseBound)
        {
            UpdateMouseBoundAnchor();
            return;
        }

        if (isFlying && anchor != null)
        {
            MoveAnchor();
            return;
        }

        if (!isAnchored)
        {
            anchor.SetCenterPosition(transform.position);
        }
    }

    public void LaunchToScreenPosition(Vector2 screenPosition)
    {
        if (anchor == null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(screenPosition);
        mouseWorld.z = 0f;
        LaunchToWorldPosition(mouseWorld);
    }

    public void LaunchToWorldPosition(Vector3 worldPosition)
    {
        if (anchor == null)
        {
            return;
        }

        anchor.Configure(anchorRadius, freezeRadius, freezableMask, transform);
        anchor.gameObject.SetActive(true);
        anchor.SetActive(false);
        anchor.SetCenterPosition(transform.position);
        targetPosition = worldPosition;
        isMouseBound = false;
        isFlying = true;
        isAnchored = false;
    }

    public void BindAnchorToMouse(Vector2 screenPosition)
    {
        if (anchor == null || Camera.main == null)
        {
            return;
        }

        mouseBoundScreenPosition = screenPosition;
        anchor.Configure(anchorRadius, freezeRadius, freezableMask, transform);
        anchor.gameObject.SetActive(true);
        anchor.SetActive(true);
        isMouseBound = true;
        isFlying = false;
        isAnchored = false;
        UpdateMouseBoundAnchor();
    }

    public void RetractAnchor()
    {
        if (anchor == null)
        {
            return;
        }

        ResetAnchorToOwner();
    }

    private void MoveAnchor()
    {
        Vector3 nextPosition = Vector3.MoveTowards(
            anchor.CircleCenterPosition,
            targetPosition,
            flightSpeed * Time.deltaTime);

        anchor.SetCenterPosition(nextPosition);

        if (Vector3.Distance(anchor.CircleCenterPosition, targetPosition) > 0.01f)
        {
            return;
        }

        anchor.SetCenterPosition(targetPosition);
        anchor.SetActive(true);
        isFlying = false;
        isMouseBound = false;
        isAnchored = true;
    }

    private void UpdateMouseBoundAnchor()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(mouseBoundScreenPosition);
        mouseWorld.z = 0f;
        anchor.SetCenterPosition(mouseWorld);
    }

    private void ResetAnchorToOwner()
    {
        isFlying = false;
        isAnchored = false;
        isMouseBound = false;
        anchor.SetActive(false);
        anchor.SetCenterPosition(transform.position);
        anchor.gameObject.SetActive(false);
    }
}
