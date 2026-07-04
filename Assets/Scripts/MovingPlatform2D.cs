using SKCell;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform2D : MonoBehaviour, IAnchorFreezable
{
    [Header("Movement")]
    [SerializeField] private float endpointPauseDuration = 0.5f;

    private SKPathDesigner pathDesigner;

    private Rigidbody2D body;
    private bool frozen;
    private Vector3 previousPosition;

    public Vector2 Velocity { get; private set; }

    private void Reset()
    {
        pathDesigner = GetComponent<SKPathDesigner>();
        body = GetComponent<Rigidbody2D>();
        SyncEndpointPause();
    }

    private void OnValidate()
    {
        endpointPauseDuration = Mathf.Max(0f, endpointPauseDuration);
        CacheComponents();
        SyncEndpointPause();
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
        pathDesigner.selfWaitTime = endpointPauseDuration;

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

        SyncEndpointPause();
        pathDesigner.UpdateDistances();
        pathDesigner.UpdateBezier();
    }

    private void Awake()
    {
        CacheComponents();
        ConfigureBody();
        SyncEndpointPause();
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
        SyncEndpointPause();
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

    private void SyncEndpointPause()
    {
        if (pathDesigner == null)
        {
            return;
        }

        pathDesigner.selfWaitTime = endpointPauseDuration;
        for (int i = 0; i < pathDesigner.waypoints.Count; i++)
        {
            pathDesigner.waypoints[i].stayTime = i == pathDesigner.waypoints.Count - 1
                ? endpointPauseDuration
                : 0f;
        }
    }
}
