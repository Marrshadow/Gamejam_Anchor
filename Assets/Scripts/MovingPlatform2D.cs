using SKCell;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform2D : MonoBehaviour, IAnchorFreezable
{
    private SKPathDesigner pathDesigner;

    private Rigidbody2D body;
    private bool frozen;
    private Vector3 previousPosition;

    public Vector2 Velocity { get; private set; }

    private void Reset()
    {
        pathDesigner = GetComponent<SKPathDesigner>();
        body = GetComponent<Rigidbody2D>();
    }

    public void Configure(Transform[] points, float moveSpeed)
    {
        CacheComponents();

        if (pathDesigner == null)
        {
            return;
        }

        pathDesigner.speed = moveSpeed;
        pathDesigner.waypoints.Clear();

        if (points == null)
        {
            return;
        }

        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null)
            {
                continue;
            }

            pathDesigner.waypoints.Add(new SKTranslatorWaypoint
            {
                localPosition = points[i].position - transform.position,
                rotation = Quaternion.identity,
                type = SKTranslatorWaypointType.Line,
                bezier = new SKBezier(),
                curve = SKCurve.LinearIn,
                stayTime = 0f
            });
        }

        pathDesigner.UpdateDistances();
        pathDesigner.UpdateBezier();
    }

    private void Awake()
    {
        CacheComponents();
        ConfigureBody();
        previousPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (frozen || Time.deltaTime <= 0f)
        {
            Velocity = Vector2.zero;
            previousPosition = transform.position;
            return;
        }

        Velocity = ((Vector2)(transform.position - previousPosition)) / Time.deltaTime;
        previousPosition = transform.position;
    }

    private void OnEnable()
    {
        AnchorFreezeZone.RegisterFreezable(this);
    }

    private void Start()
    {
        AnchorFreezeZone.RegisterFreezable(this);
        ApplyPathFrozenState();
    }

    private void OnDisable()
    {
        AnchorFreezeZone.UnregisterFreezable(this);
    }

    public void SetFrozen(bool freeze)
    {
        if (frozen == freeze)
        {
            return;
        }

        frozen = freeze;
        CacheComponents();
        ConfigureBody();

        if (pathDesigner != null)
        {
            ApplyPathFrozenState();
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        if (freeze)
        {
            Velocity = Vector2.zero;
            previousPosition = transform.position;
        }
    }

    private void CacheComponents()
    {
        if (pathDesigner == null)
        {
            pathDesigner = GetComponent<SKPathDesigner>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }

    private void ConfigureBody()
    {
        if (body == null)
        {
            return;
        }

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void ApplyPathFrozenState()
    {
        if (pathDesigner == null)
        {
            return;
        }

        if (frozen)
        {
            pathDesigner.PausePath();
            pathDesigner.enabled = false;
            return;
        }

        pathDesigner.enabled = true;
        pathDesigner.ResumePath();
    }
}
