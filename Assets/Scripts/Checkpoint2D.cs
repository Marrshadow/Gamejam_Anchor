using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Checkpoint2D : MonoBehaviour
{
    [SerializeField] private Transform respawnPoint;

    private Collider2D checkpointCollider;

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
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.CompareTag("Player"))
        {
            return;
        }

        Vector3 nextRespawnPosition = respawnPoint != null
            ? respawnPoint.position
            : transform.position;
        RespawnTrap2D.SetDefaultRespawnPosition(nextRespawnPosition);
    }

    private void CacheCollider()
    {
        if (checkpointCollider == null)
        {
            checkpointCollider = GetComponent<Collider2D>();
        }
    }

    private void ConfigureCollider()
    {
        if (checkpointCollider != null)
        {
            checkpointCollider.isTrigger = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 targetPosition = respawnPoint != null
            ? respawnPoint.position
            : transform.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(targetPosition, 0.25f);
    }
}
