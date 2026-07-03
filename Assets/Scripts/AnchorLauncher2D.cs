using UnityEngine;

public class AnchorLauncher2D : MonoBehaviour
{
    [Header("Anchor")]
    [SerializeField] private AnchorFreezeZone anchor;
    [SerializeField] private float flightSpeed = 18f;
    [SerializeField] private float anchorRadius = 0.18f;
    [SerializeField] private float freezeRadius = 2.4f;
    [SerializeField] private LayerMask freezableMask = ~0;
    [SerializeField] private Color anchorColor = new Color(0.15f, 0.85f, 1f, 1f);

    private Vector3 targetPosition;
    private bool isFlying;
    private bool isAnchored;

    private void Awake()
    {
        if (anchor == null)
        {
            return;
        }

        anchor.Configure(anchorRadius, freezeRadius, anchorColor, freezableMask, transform);
        ResetAnchorToOwner();
    }

    private void Update()
    {
        if (anchor == null)
        {
            return;
        }

        if (isFlying && anchor != null)
        {
            MoveAnchor();
            return;
        }

        if (!isAnchored)
        {
            anchor.transform.position = transform.position;
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

        anchor.Configure(anchorRadius, freezeRadius, anchorColor, freezableMask, transform);
        anchor.gameObject.SetActive(true);
        anchor.SetActive(false);
        anchor.transform.position = transform.position;
        targetPosition = worldPosition;
        isFlying = true;
        isAnchored = false;
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
        anchor.transform.position = Vector3.MoveTowards(
            anchor.transform.position,
            targetPosition,
            flightSpeed * Time.deltaTime);

        if (Vector3.Distance(anchor.transform.position, targetPosition) > 0.01f)
        {
            return;
        }

        anchor.transform.position = targetPosition;
        anchor.SetActive(true);
        isFlying = false;
        isAnchored = true;
    }

    private void ResetAnchorToOwner()
    {
        isFlying = false;
        isAnchored = false;
        anchor.SetActive(false);
        anchor.transform.position = transform.position;
        anchor.gameObject.SetActive(false);
    }
}
