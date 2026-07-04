using SKCell;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBehaviour : MonoBehaviour, IAnchorFreezable
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private bool startsMovingRight = false;
    [SerializeField] private float gravityScale = 3f;

    [Header("Detection")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float wallCheckDistance = 0.2f;
    [SerializeField] private float ledgeCheckDistance = 0.7f;
    [SerializeField] private float minTurnInterval = 0.15f;
    [SerializeField] private Vector2 wallCheckOffset = new Vector2(0.28f, 0.02f);
    [SerializeField] private Vector2 ledgeCheckOffset = new Vector2(0.28f, -0.1f);

    private SKPathDesigner pathDesigner;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Rigidbody2D body;
    private Collider2D[] ownColliders;
    [SerializeField] private bool spriteFacesRight;

    private bool frozen;
    private int moveDirection;
    private float animatorSpeed = 1f;
    private float cachedGravityScale = 1f;
    private float lastTurnTime = -999f;

    private void Reset()
    {
        pathDesigner = GetComponent<SKPathDesigner>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        body = GetComponent<Rigidbody2D>();
        ownColliders = GetComponentsInChildren<Collider2D>();
    }

    private void Awake()
    {
        if (groundLayer.value == 0 || groundLayer.value == -1)
        {
            groundLayer = LayerMask.GetMask("Ground");
        }

        if (pathDesigner == null)
        {
            pathDesigner = GetComponent<SKPathDesigner>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        ownColliders = GetComponentsInChildren<Collider2D>();

        if (animator != null)
        {
            animatorSpeed = animator.speed;
        }

        if (pathDesigner != null)
        {
            pathDesigner.enabled = false;
        }

        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            cachedGravityScale = gravityScale > 0f ? gravityScale : Mathf.Max(1f, body.gravityScale);
            body.gravityScale = cachedGravityScale;
        }

        moveDirection = startsMovingRight ? 1 : -1;
        ApplyFacing(moveDirection);
    }

    private void OnEnable()
    {
        AnchorFreezeZone.RegisterFreezable(this);
    }

    private void Start()
    {
        AnchorFreezeZone.RegisterFreezable(this);
    }

    private void OnDisable()
    {
        AnchorFreezeZone.UnregisterFreezable(this);
    }

    private void FixedUpdate()
    {
        if (body == null)
        {
            return;
        }

        if (frozen)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        if (Time.time - lastTurnTime >= minTurnInterval && ShouldTurnAround())
        {
            moveDirection *= -1;
            lastTurnTime = Time.time;
            ApplyFacing(moveDirection);
        }

        Vector2 platformVelocity = Vector2.zero;
        bool isOnMovingPlatform = TryGetStandingPlatformVelocity(out platformVelocity);
        Vector2 velocity = body.linearVelocity;
        velocity.x = moveDirection * moveSpeed + platformVelocity.x;
        if (isOnMovingPlatform && platformVelocity.y > velocity.y)
        {
            velocity.y = platformVelocity.y;
        }

        body.linearVelocity = velocity;
    }

    public void SetFrozen(bool freeze)
    {
        if (frozen == freeze)
        {
            return;
        }

        frozen = freeze;

        if (pathDesigner != null)
        {
            pathDesigner.enabled = false;
        }

        if (animator != null)
        {
            if (freeze)
            {
                animatorSpeed = animator.speed;
            }

            animator.speed = freeze ? 0f : animatorSpeed;
        }

        if (body != null)
        {
            if (freeze)
            {
                cachedGravityScale = body.gravityScale;
            }

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = freeze ? 0f : Mathf.Max(1f, cachedGravityScale);
        }
    }

    private void ApplyFacing(float horizontalDelta)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        bool movingRight = horizontalDelta > 0f;
        spriteRenderer.flipX = movingRight != spriteFacesRight;
    }

    private bool ShouldTurnAround()
    {
        Vector2 position = body != null ? body.position : (Vector2)transform.position;
        Vector2 wallOrigin = position + new Vector2(wallCheckOffset.x * moveDirection, wallCheckOffset.y);
        Vector2 ledgeOrigin = position + new Vector2(ledgeCheckOffset.x * moveDirection, ledgeCheckOffset.y);

        bool hasWallAhead = HasSolidWallAhead(wallOrigin);
        bool hasGroundAhead = HasGroundAhead(ledgeOrigin);
        return hasWallAhead || !hasGroundAhead;
    }

    private bool HasSolidWallAhead(Vector2 origin)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.right * moveDirection, wallCheckDistance, groundLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.isTrigger || IsOwnCollider(hitCollider))
            {
                continue;
            }

            if (hitCollider.GetComponentInParent<Fog>() != null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool HasGroundAhead(Vector2 origin)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, ledgeCheckDistance, groundLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.isTrigger || IsOwnCollider(hitCollider))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool TryGetStandingPlatformVelocity(out Vector2 platformVelocity)
    {
        platformVelocity = Vector2.zero;
        Vector2 position = body != null ? body.position : (Vector2)transform.position;
        Vector2 probeOrigin = position + new Vector2(0f, ledgeCheckOffset.y);
        RaycastHit2D[] hits = Physics2D.RaycastAll(probeOrigin, Vector2.down, ledgeCheckDistance, groundLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.isTrigger || IsOwnCollider(hitCollider))
            {
                continue;
            }

            MovingPlatform2D platform = hitCollider.GetComponentInParent<MovingPlatform2D>();
            if (platform != null)
            {
                platformVelocity = platform.Velocity;
                return true;
            }
        }

        return false;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryRespawn(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryRespawn(other);
    }

    private void TryRespawn(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.CompareTag("Player"))
        {
            return;
        }

        RespawnTrap2D.RespawnToDefault(player);
    }

    private bool IsOwnCollider(Collider2D candidate)
    {
        if (candidate == null || ownColliders == null)
        {
            return false;
        }

        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (ownColliders[i] == candidate)
            {
                return true;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        int direction = Application.isPlaying ? moveDirection : (startsMovingRight ? 1 : -1);
        Vector2 position = Application.isPlaying && body != null ? body.position : (Vector2)transform.position;
        Vector2 wallOrigin = position + new Vector2(wallCheckOffset.x * direction, wallCheckOffset.y);
        Vector2 ledgeOrigin = position + new Vector2(ledgeCheckOffset.x * direction, ledgeCheckOffset.y);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(wallOrigin, wallOrigin + Vector2.right * direction * wallCheckDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(ledgeOrigin, ledgeOrigin + Vector2.down * ledgeCheckDistance);
    }
}
