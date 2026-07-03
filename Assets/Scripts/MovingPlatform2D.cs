using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform2D : MonoBehaviour, IAnchorFreezable
{
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float speed = 2f;
    [SerializeField] private float waitTime = 0.25f;

    private Rigidbody2D body;
    private int targetIndex = 1;
    private float waitTimer;
    private bool frozen;
    private Vector2 frozenVelocity;

    public void Configure(Transform[] points, float moveSpeed)
    {
        waypoints = points;
        speed = moveSpeed;
        targetIndex = points != null && points.Length > 1 ? 1 : 0;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void FixedUpdate()
    {
        if (frozen || waypoints == null || waypoints.Length < 2)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            body.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 target = waypoints[targetIndex].position;
        Vector2 next = Vector2.MoveTowards(body.position, target, speed * Time.fixedDeltaTime);
        body.MovePosition(next);

        if (Vector2.Distance(next, target) <= 0.01f)
        {
            targetIndex = (targetIndex + 1) % waypoints.Length;
            waitTimer = waitTime;
        }
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
}
