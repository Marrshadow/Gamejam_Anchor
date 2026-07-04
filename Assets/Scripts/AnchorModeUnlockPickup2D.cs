using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AnchorModeUnlockPickup2D : MonoBehaviour
{
    [SerializeField] private bool deactivateOnPickup = true;
    [SerializeField] private GameObject visualRoot;

    private Collider2D pickupCollider;
    private bool collected;

    private void Reset()
    {
        CacheCollider();
        ConfigureCollider();
    }

    private void Awake()
    {
        CacheCollider();
        ConfigureCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        collected = true;
        player.UnlockAnchorModeSwitch();

        if (!deactivateOnPickup)
        {
            SetCollectedVisualState();
            return;
        }

        gameObject.SetActive(false);
    }

    private void CacheCollider()
    {
        if (pickupCollider == null)
        {
            pickupCollider = GetComponent<Collider2D>();
        }
    }

    private void ConfigureCollider()
    {
        if (pickupCollider != null)
        {
            pickupCollider.isTrigger = true;
        }
    }

    private void SetCollectedVisualState()
    {
        if (visualRoot != null)
        {
            visualRoot.SetActive(false);
        }

        if (pickupCollider != null)
        {
            pickupCollider.enabled = false;
        }
    }
}
