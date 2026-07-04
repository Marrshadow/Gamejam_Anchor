using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PatrolEnemy2D : MonoBehaviour, IAnchorFreezable
{
    [SerializeField] private Transform leftLimit;
    [SerializeField] private Transform rightLimit;
    [SerializeField] private float speed = 4f;
    [SerializeField] private bool startsMovingRight = true;

    private const float DefaultPatrolHalfWidth = 10f;

    private Rigidbody2D body;
    private Vector2 startPoint;
    private Vector2 endPoint;
    private Vector2 targetPoint;
    private int direction = 1;
    private bool frozen;
    private Vector2 frozenVelocity;

    public void Configure(Transform left, Transform right, float patrolSpeed)
    {
        leftLimit = left;
        rightLimit = right;
        speed = patrolSpeed;
        CachePatrolPointsFromStartTransform();
        ApplyInitialDirection();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.freezeRotation = true;
        CachePatrolPointsFromStartTransform();
        ApplyInitialDirection();
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
        if (frozen)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 next = Vector2.MoveTowards(body.position, targetPoint, speed * Time.fixedDeltaTime);

        body.MovePosition(next);
        if (Vector2.Distance(next, targetPoint) <= 0.01f)
        {
            direction *= -1;
            targetPoint = direction > 0 ? endPoint : startPoint;
        }

        ApplyFacingDirection();
    }

    public void SetFrozen(bool freeze)
    {
        if (frozen == freeze)
        {
            return;
        }

        frozen = freeze;
        if (freeze)
        {
            frozenVelocity = body.linearVelocity;
            body.linearVelocity = Vector2.zero;
        }
        else
        {
            body.linearVelocity = frozenVelocity;
        }
    }

    private void CachePatrolPointsFromStartTransform()
    {
        if (leftLimit != null && rightLimit != null)
        {
            startPoint = leftLimit.position;
            endPoint = rightLimit.position;
            return;
        }

        Vector2 position = transform.position;
        startPoint = position + Vector2.left * DefaultPatrolHalfWidth;
        endPoint = position + Vector2.right * DefaultPatrolHalfWidth;
    }

    private void ApplyInitialDirection()
    {
        direction = startsMovingRight ? 1 : -1;
        targetPoint = direction > 0 ? endPoint : startPoint;
        ApplyFacingDirection();
    }

    private void ApplyFacingDirection()
    {
        Vector2 currentPosition = body != null ? body.position : (Vector2)transform.position;
        float horizontalDirection = targetPoint.x - currentPosition.x;
        if (Mathf.Abs(horizontalDirection) <= 0.01f)
        {
            return;
        }

        float facingSign = horizontalDirection > 0f ? -1f : 1f;
        transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * facingSign, transform.localScale.y, transform.localScale.z);
    }

    private void OnDrawGizmosSelected()
    {
        CachePatrolPointsFromStartTransform();
        Gizmos.color = Color.red;
        Gizmos.DrawLine(startPoint, endPoint);
    }
}
