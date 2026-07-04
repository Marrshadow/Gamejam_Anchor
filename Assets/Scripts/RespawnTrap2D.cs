using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class RespawnTrap2D : MonoBehaviour
{
    [SerializeField] private Transform respawnTarget;

    private static Vector3 defaultRespawnPosition;
    private static bool hasDefaultRespawnPosition;

    private Collider2D trapCollider;
    private Vector3 initialPlayerPosition;
    private bool hasInitialPlayerPosition;

    private void Reset()
    {
        CacheCollider();
        ConfigureCollider();
    }

    private void Awake()
    {
        CacheCollider();
        ConfigureCollider();
        CacheInitialPlayerPosition();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.CompareTag("Player"))
        {
            return;
        }

        RespawnPlayer(player.transform);
    }

    private void RespawnPlayer(Transform player)
    {
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
        }

        player.position = respawnTarget != null
            ? respawnTarget.position
            : initialPlayerPosition;
    }

    public static bool RespawnToDefault(PlayerController player)
    {
        if (player == null)
        {
            return false;
        }

        EnsureDefaultRespawnPosition();
        if (!hasDefaultRespawnPosition)
        {
            return false;
        }

        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
        }

        player.transform.position = defaultRespawnPosition;
        return true;
    }

    public static void SetDefaultRespawnPosition(Vector3 position)
    {
        defaultRespawnPosition = position;
        hasDefaultRespawnPosition = true;
    }

    private void CacheCollider()
    {
        if (trapCollider == null)
        {
            trapCollider = GetComponent<Collider2D>();
        }
    }

    private void ConfigureCollider()
    {
        if (trapCollider != null)
        {
            trapCollider.isTrigger = true;
        }
    }

    private void CacheInitialPlayerPosition()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            initialPlayerPosition = Vector3.zero;
            hasInitialPlayerPosition = false;
            return;
        }

        initialPlayerPosition = player.transform.position;
        hasInitialPlayerPosition = true;
        if (!hasDefaultRespawnPosition)
        {
            SetDefaultRespawnPosition(initialPlayerPosition);
        }
    }

    private static void EnsureDefaultRespawnPosition()
    {
        if (hasDefaultRespawnPosition)
        {
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            return;
        }

        defaultRespawnPosition = player.transform.position;
        hasDefaultRespawnPosition = true;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 targetPosition = respawnTarget != null
            ? respawnTarget.position
            : hasInitialPlayerPosition ? initialPlayerPosition : Vector3.zero;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, Vector3.one);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(targetPosition, 0.25f);
    }
}
