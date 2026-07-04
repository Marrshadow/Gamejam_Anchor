using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ChargingEnemy2D : MonoBehaviour, IAnchorFreezable
{
    private enum ChargeState
    {
        Watching,
        Preparing,
        Dashing,
        Cooldown
    }

    [Header("Detection")]
    [SerializeField] private LayerMask playerLayer = ~0;
    [SerializeField] private LayerMask obstacleLayer = ~0;
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float detectionRange = 65f;
    [SerializeField] private float verticalDetectionTolerance = 0.45f;
    [SerializeField] private Vector2 eyeOffset = new Vector2(0f, 0.15f);

    [Header("Charge")]
    [SerializeField] private float prepareDuration = 0.35f;
    [SerializeField] private float dashCooldown = 5f;
    [SerializeField] private float dashSpeed = 8f;
    [SerializeField] private float maxGroundDashDistance = 4f;
    [SerializeField] private float wallCheckDistance = 0.12f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private Vector2 wallCheckOffset = new Vector2(0.35f, 0f);
    [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -0.5f);

    [Header("Visual")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;
    [SerializeField] private bool spriteFacesRight = true;

    [Header("Contact")]
    [SerializeField] private Collider2D respawnCollider;

    private Rigidbody2D body;
    private Collider2D[] ownColliders;
    private ChargeState state;
    private int facingDirection = 1;
    private bool frozen;
    private bool airborneDash;
    private float prepareTimer;
    private float cooldownTimer;
    private float dashStartX;
    private float dashTargetX;
    private float dashLockedY;
    private float cachedGravityScale = 1f;
    private float animatorSpeed = 1f;

    private void OnValidate()
    {
        detectionRange = Mathf.Max(65f, detectionRange);
        dashCooldown = Mathf.Max(0f, dashCooldown);
        groundCheckDistance = Mathf.Max(0.01f, groundCheckDistance);
    }

    private void Reset()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
        ownColliders = GetComponentsInChildren<Collider2D>();
        respawnCollider = GetComponentInChildren<CapsuleCollider2D>();
    }

    private void Awake()
    {
        if (playerLayer.value == 0 || playerLayer.value == -1)
        {
            playerLayer = LayerMask.GetMask("Player");
        }

        if (groundLayer.value == 0 || groundLayer.value == -1)
        {
            groundLayer = LayerMask.GetMask("Ground");
        }

        if (obstacleLayer.value == 0 || obstacleLayer.value == -1)
        {
            obstacleLayer = groundLayer;
        }

        body = GetComponent<Rigidbody2D>();
        ownColliders = GetComponentsInChildren<Collider2D>();
        ResolveRespawnCollider();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animatorSpeed = animator.speed;
        }

        body.bodyType = RigidbodyType2D.Dynamic;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        cachedGravityScale = Mathf.Max(0.01f, body.gravityScale);
        ApplyFacing(facingDirection);
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

        if (state == ChargeState.Dashing)
        {
            LockDashYPosition();
        }

        if (frozen)
        {
            return;
        }

        switch (state)
        {
            case ChargeState.Watching:
                UpdateWatching();
                break;
            case ChargeState.Preparing:
                UpdatePreparing();
                break;
            case ChargeState.Dashing:
                UpdateDashing();
                break;
            case ChargeState.Cooldown:
                UpdateCooldown();
                break;
        }
    }

    private void LateUpdate()
    {
        if (state == ChargeState.Dashing)
        {
            LockDashYPosition();
        }
    }

    public void SetFrozen(bool freeze)
    {
        if (frozen == freeze)
        {
            return;
        }

        frozen = freeze;
        if (body != null)
        {
            if (freeze)
            {
                cachedGravityScale = body.gravityScale;
            }

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = freeze ? 0f : Mathf.Max(0.01f, cachedGravityScale);

            if (state == ChargeState.Dashing)
            {
                LockDashYPosition();
            }
        }

        if (animator != null)
        {
            if (freeze)
            {
                animatorSpeed = animator.speed;
            }

            animator.speed = freeze ? 0f : animatorSpeed;
        }
    }

    private void UpdateWatching()
    {
        EnsureNormalGravity();

        if (!TryFindPlayerHorizontally(out int playerDirection, out float playerDistance))
        {
            return;
        }

        facingDirection = playerDirection;
        ApplyFacing(facingDirection);
        dashStartX = body.position.x;
        dashTargetX = body.position.x + facingDirection * Mathf.Min(maxGroundDashDistance, playerDistance);
        prepareTimer = prepareDuration;
        state = ChargeState.Preparing;
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
    }

    private void UpdatePreparing()
    {
        EnsureNormalGravity();
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        prepareTimer -= Time.fixedDeltaTime;
        if (prepareTimer > 0f)
        {
            return;
        }

        BeginDash();
    }

    private void BeginDash()
    {
        airborneDash = false;
        dashLockedY = body.position.y;
        body.gravityScale = 0f;
        state = ChargeState.Dashing;
        LockDashYPosition();
    }

    private void UpdateDashing()
    {
        LockDashYPosition();

        if (HasObstacleAhead())
        {
            StopDash();
            return;
        }

        bool grounded = IsGrounded();
        if (!grounded)
        {
            airborneDash = true;
        }
        else if (airborneDash && IsCenterGrounded())
        {
            StopDash();
            return;
        }

        Vector2 velocity = body.linearVelocity;
        velocity.x = facingDirection * dashSpeed;
        velocity.y = 0f;
        body.linearVelocity = velocity;
        LockDashYPosition();

        if (!airborneDash && HasReachedDashTarget())
        {
            StopDash();
        }
    }

    private void StopDash()
    {
        LockDashYPosition();
        airborneDash = false;
        EnsureNormalGravity();
        body.linearVelocity = Vector2.zero;
        cooldownTimer = dashCooldown;
        state = ChargeState.Cooldown;
    }

    private void LockDashYPosition()
    {
        if (body == null)
        {
            return;
        }

        Vector2 lockedPosition = new Vector2(body.position.x, dashLockedY);
        body.position = lockedPosition;
        transform.position = new Vector3(transform.position.x, dashLockedY, transform.position.z);
        body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
    }

    private void UpdateCooldown()
    {
        EnsureNormalGravity();
        cooldownTimer -= Time.fixedDeltaTime;
        if (cooldownTimer <= 0f)
        {
            state = ChargeState.Watching;
        }
    }

    private bool HasReachedDashTarget()
    {
        return facingDirection > 0
            ? body.position.x >= dashTargetX
            : body.position.x <= dashTargetX;
    }

    private void EnsureNormalGravity()
    {
        if (body != null && !frozen)
        {
            body.gravityScale = Mathf.Max(0.01f, cachedGravityScale);
        }
    }

    private bool TryFindPlayerHorizontally(out int playerDirection, out float playerDistance)
    {
        playerDirection = facingDirection;
        playerDistance = 0f;

        float range = Mathf.Max(65f, detectionRange);
        bool foundLeft = TryFindPlayerInDirection(-1, range, out float leftDistance);
        bool foundRight = TryFindPlayerInDirection(1, range, out float rightDistance);

        if (!foundLeft && !foundRight)
        {
            return false;
        }

        if (foundRight && (!foundLeft || rightDistance <= leftDistance))
        {
            playerDirection = 1;
            playerDistance = rightDistance;
            return true;
        }

        playerDirection = -1;
        playerDistance = leftDistance;
        return true;
    }

    private bool TryFindPlayerInDirection(int direction, float range, out float playerDistance)
    {
        playerDistance = float.PositiveInfinity;

        Vector2 origin = (Vector2)transform.position + eyeOffset;
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.right * direction, range, playerLayer | obstacleLayer);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider == null || IsOwnCollider(hitCollider))
            {
                continue;
            }

            if (IsPlayer(hitCollider) && IsPlayerWithinVerticalBand(hitCollider.transform))
            {
                playerDistance = hits[i].distance;
                return true;
            }

            if (hitCollider.isTrigger || hitCollider.GetComponentInParent<Fog>() != null)
            {
                continue;
            }

            if (IsObstacle(hitCollider))
            {
                return false;
            }
        }

        return false;
    }

    private bool HasObstacleAhead()
    {
        Vector2 origin = (Vector2)transform.position + new Vector2(wallCheckOffset.x * facingDirection, wallCheckOffset.y);
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.right * facingDirection, wallCheckDistance, obstacleLayer);
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

    private bool IsGrounded()
    {
        if (TryGetFeetBounds(out Bounds feetBounds))
        {
            float inset = Mathf.Min(feetBounds.extents.x * 0.8f, 0.08f);
            Vector2 left = new Vector2(feetBounds.min.x + inset, feetBounds.min.y + 0.02f);
            Vector2 center = new Vector2(feetBounds.center.x, feetBounds.min.y + 0.02f);
            Vector2 right = new Vector2(feetBounds.max.x - inset, feetBounds.min.y + 0.02f);
            return HasGroundBelow(left) || HasGroundBelow(center) || HasGroundBelow(right);
        }

        return HasGroundBelow((Vector2)transform.position + groundCheckOffset);
    }

    private bool IsCenterGrounded()
    {
        if (TryGetFeetBounds(out Bounds feetBounds))
        {
            Vector2 center = new Vector2(feetBounds.center.x, feetBounds.min.y + 0.02f);
            return HasGroundBelow(center);
        }

        return HasGroundBelow((Vector2)transform.position + groundCheckOffset);
    }

    private bool HasGroundBelow(Vector2 origin)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, groundCheckDistance, groundLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;
            if (hitCollider != null && !hitCollider.isTrigger && !IsOwnCollider(hitCollider))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetFeetBounds(out Bounds feetBounds)
    {
        feetBounds = default;
        bool hasBounds = false;

        if (ownColliders == null)
        {
            return false;
        }

        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider2D ownCollider = ownColliders[i];
            if (ownCollider == null || ownCollider.isTrigger || !ownCollider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                feetBounds = ownCollider.bounds;
                hasBounds = true;
                continue;
            }

            feetBounds.Encapsulate(ownCollider.bounds);
        }

        return hasBounds;
    }

    private bool IsObstacle(Collider2D candidate)
    {
        return candidate != null && (obstacleLayer.value & (1 << candidate.gameObject.layer)) != 0;
    }

    private bool IsPlayer(Collider2D candidate)
    {
        return candidate != null
            && (candidate.GetComponentInParent<PlayerController>() != null || candidate.CompareTag("Player"));
    }

    private bool IsPlayerWithinVerticalBand(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        PlayerController player = candidate.GetComponentInParent<PlayerController>();
        Vector3 playerPosition = player != null ? player.transform.position : candidate.position;
        return Mathf.Abs(playerPosition.y - transform.position.y) <= verticalDetectionTolerance;
    }

    private void ApplyFacing(int direction)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        bool movingRight = direction > 0;
        spriteRenderer.flipX = movingRight != spriteFacesRight;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (TryRespawnPlayer(collision.collider, collision.otherCollider, collision.collider))
        {
            return;
        }

        if (state == ChargeState.Dashing && IsBlockingDashCollision(collision))
        {
            StopDash();
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (TryRespawnPlayer(collision.collider, collision.otherCollider, collision.collider))
        {
            return;
        }

        if (state == ChargeState.Dashing && IsBlockingDashCollision(collision))
        {
            StopDash();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryRespawnPlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryRespawnPlayer(other);
    }

    private bool IsBlockingDashCollision(Collision2D collision)
    {
        if (!IsObstacle(collision.collider))
        {
            return false;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);
            if (Vector2.Dot(contact.normal, Vector2.left * facingDirection) > 0.5f)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryRespawnPlayer(Collider2D other, Collider2D firstOwnColliderCandidate = null, Collider2D secondOwnColliderCandidate = null)
    {
        if (other == null || !IsRespawnContact(other, firstOwnColliderCandidate, secondOwnColliderCandidate))
        {
            return false;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.CompareTag("Player"))
        {
            return false;
        }

        RespawnTrap2D.RespawnToDefault(player);
        return true;
    }

    private bool IsRespawnContact(Collider2D other, Collider2D firstOwnColliderCandidate, Collider2D secondOwnColliderCandidate)
    {
        ResolveRespawnCollider();
        if (respawnCollider == null)
        {
            return false;
        }

        if (firstOwnColliderCandidate == respawnCollider || secondOwnColliderCandidate == respawnCollider)
        {
            return true;
        }

        return respawnCollider.isTrigger && other != null && respawnCollider.IsTouching(other);
    }

    private void ResolveRespawnCollider()
    {
        if (respawnCollider != null)
        {
            return;
        }

        respawnCollider = GetComponentInChildren<CapsuleCollider2D>();
    }

    private bool IsRespawnCollider(Collider2D ownCollider)
    {
        ResolveRespawnCollider();
        return respawnCollider != null && ownCollider == respawnCollider;
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
        Vector2 eyeOrigin = (Vector2)transform.position + eyeOffset;
        int direction = Application.isPlaying ? facingDirection : 1;
        Vector2 wallOrigin = (Vector2)transform.position + new Vector2(wallCheckOffset.x * direction, wallCheckOffset.y);
        Vector2 groundOrigin = TryGetFeetBounds(out Bounds feetBounds)
            ? new Vector2(feetBounds.center.x, feetBounds.min.y + 0.02f)
            : (Vector2)transform.position + groundCheckOffset;

        Gizmos.color = Color.cyan;
        float range = Mathf.Max(65f, detectionRange);
        Gizmos.DrawLine(eyeOrigin, eyeOrigin + Vector2.right * range);
        Gizmos.DrawLine(eyeOrigin, eyeOrigin + Vector2.left * range);
        Gizmos.color = Color.red;
        Gizmos.DrawLine(wallOrigin, wallOrigin + Vector2.right * direction * wallCheckDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(groundOrigin, groundOrigin + Vector2.down * groundCheckDistance);
    }
}
