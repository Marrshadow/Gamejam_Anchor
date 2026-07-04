using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Fog : MonoBehaviour
{
    [SerializeField] private float hiddenDuration = 2f;

    private Collider2D[] fogColliders;
    private SpriteRenderer[] spriteRenderers;
    private bool hidden;
    private float hiddenTimer;

    private void Reset()
    {
        CacheComponents();
        ConfigureColliders();
    }

    private void Awake()
    {
        CacheComponents();
        ConfigureColliders();
        SetHidden(false);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleContact(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        HandleContact(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleContact(other);
    }

    private void Update()
    {
        if (!hidden)
        {
            return;
        }

        hiddenTimer -= Time.deltaTime;
        if (hiddenTimer <= 0f)
        {
            SetHidden(false);
        }
    }

    private void CacheComponents()
    {
        if (fogColliders == null || fogColliders.Length == 0)
        {
            fogColliders = GetComponents<Collider2D>();
        }

        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }
    }

    private void ConfigureColliders()
    {
        if (fogColliders == null)
        {
            return;
        }

        for (int i = 0; i < fogColliders.Length; i++)
        {
            if (fogColliders[i] != null)
            {
                fogColliders[i].isTrigger = true;
            }
        }
    }

    private void HandleContact(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null && player.CompareTag("Player"))
        {
            RespawnTrap2D.RespawnToDefault(player);
            return;
        }

        if (hidden || !IsEnemy(other))
        {
            return;
        }

        hiddenTimer = hiddenDuration;
        SetHidden(true);
    }

    private bool IsEnemy(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        return other.GetComponentInParent<EnemyBehaviour>() != null
            || other.GetComponentInParent<PatrolEnemy2D>() != null
            || other.CompareTag("Enemy");
    }

    private void SetHidden(bool shouldHide)
    {
        hidden = shouldHide;

        if (spriteRenderers != null)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    spriteRenderers[i].enabled = !shouldHide;
                }
            }
        }

        if (fogColliders != null)
        {
            for (int i = 0; i < fogColliders.Length; i++)
            {
                if (fogColliders[i] != null)
                {
                    fogColliders[i].enabled = !shouldHide;
                }
            }
        }
    }
}
