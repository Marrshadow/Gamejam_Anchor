using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PatrolEnemy2D : MonoBehaviour, IAnchorFreezable
{
    [SerializeField] private Transform leftLimit;
    [SerializeField] private Transform rightLimit;
    [SerializeField] private float patrolHalfWidth = 3f;
    [SerializeField] private float speed = 2f;

    private Rigidbody2D body;
    private float leftX;
    private float rightX;
    private int direction = 1;
    private bool frozen;
    private Vector2 frozenVelocity;

    public void Configure(Transform left, Transform right, float patrolSpeed)
    {
        leftLimit = left;
        rightLimit = right;
        speed = patrolSpeed;
        CacheBounds();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.freezeRotation = true;
        CacheBounds();
    }

    private void FixedUpdate()
    {
        if (frozen)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        CacheBounds();
        Vector2 next = body.position + Vector2.right * direction * speed * Time.fixedDeltaTime;

        if (next.x >= rightX)
        {
            next.x = rightX;
            direction = -1;
        }
        else if (next.x <= leftX)
        {
            next.x = leftX;
            direction = 1;
        }

        body.MovePosition(next);
        transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x) * direction, transform.localScale.y, transform.localScale.z);
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

    private void CacheBounds()
    {
        if (leftLimit != null && rightLimit != null)
        {
            leftX = Mathf.Min(leftLimit.position.x, rightLimit.position.x);
            rightX = Mathf.Max(leftLimit.position.x, rightLimit.position.x);
            return;
        }

        leftX = transform.position.x - patrolHalfWidth;
        rightX = transform.position.x + patrolHalfWidth;
    }

    private void OnDrawGizmosSelected()
    {
        CacheBounds();
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(leftX, transform.position.y, 0f), new Vector3(rightX, transform.position.y, 0f));
    }
}
