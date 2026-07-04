using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class MouseRangeCircle2D : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private float radius = 4f;
    [SerializeField] private int segments = 96;
    [SerializeField] private bool showCircle = true;
    [SerializeField] private float lineWidth = 0.04f;
    [SerializeField] private Color lineColor = new Color(0.35f, 0.9f, 1f, 0.7f);

    private LineRenderer lineRenderer;
    private Material runtimeMaterial;
    private bool circleDirty = true;

    public float Radius
    {
        get => radius;
        set
        {
            radius = Mathf.Max(0.01f, value);
            circleDirty = true;
        }
    }

    public Vector3 CenterPosition => player != null ? player.position : transform.position;

    private void Reset()
    {
        CacheLineRenderer();
        ConfigureLineRenderer();
        DrawCircle();
    }

    private void Awake()
    {
        CacheLineRenderer();
        ConfigureLineRenderer();
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.01f, radius);
        segments = Mathf.Max(8, segments);
        lineWidth = Mathf.Max(0.001f, lineWidth);
        circleDirty = true;

        CacheLineRenderer();
        ConfigureLineRenderer();
        DrawCircle();
    }

    private void LateUpdate()
    {
        FollowPlayer();

        if (circleDirty)
        {
            DrawCircle();
        }
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            if (Application.isPlaying)
            {
                Destroy(runtimeMaterial);
            }
            else
            {
                DestroyImmediate(runtimeMaterial);
            }
        }
    }

    public void BindPlayer(Transform nextPlayer)
    {
        player = nextPlayer;
        FollowPlayer();
        circleDirty = true;
    }

    public Vector3 ScreenToClampedWorld(Vector2 screenPosition, Camera worldCamera)
    {
        if (!TryScreenToWorld(screenPosition, worldCamera, out Vector3 worldPosition))
        {
            return CenterPosition;
        }

        return ClampWorldPosition(worldPosition);
    }

    public Vector3 ClampWorldPosition(Vector3 worldPosition)
    {
        Vector3 center = CenterPosition;
        Vector2 offset = worldPosition - center;
        float maxDistanceSquared = radius * radius;

        if (offset.sqrMagnitude <= maxDistanceSquared)
        {
            worldPosition.z = center.z;
            return worldPosition;
        }

        Vector2 clampedOffset = offset.normalized * radius;
        return new Vector3(center.x + clampedOffset.x, center.y + clampedOffset.y, center.z);
    }

    private void CacheLineRenderer()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }
    }

    private void ConfigureLineRenderer()
    {
        if (lineRenderer == null)
        {
            return;
        }

        if (runtimeMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                runtimeMaterial = new Material(shader)
                {
                    hideFlags = HideFlags.DontSave
                };
            }
        }

        lineRenderer.enabled = showCircle;
        lineRenderer.loop = true;
        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = Mathf.Max(8, segments);
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;

        if (runtimeMaterial != null)
        {
            lineRenderer.sharedMaterial = runtimeMaterial;
        }
    }

    private void FollowPlayer()
    {
        if (player == null)
        {
            return;
        }

        transform.position = player.position;
        circleDirty = true;
    }

    private bool TryScreenToWorld(Vector2 screenPosition, Camera worldCamera, out Vector3 worldPosition)
    {
        worldPosition = CenterPosition;
        if (worldCamera == null)
        {
            return false;
        }

        Vector3 center = CenterPosition;
        float depth = Mathf.Abs(worldCamera.transform.position.z - center.z);
        Vector3 screenPoint = new Vector3(screenPosition.x, screenPosition.y, depth);
        worldPosition = worldCamera.ScreenToWorldPoint(screenPoint);
        worldPosition.z = center.z;
        return true;
    }

    private void DrawCircle()
    {
        if (lineRenderer == null)
        {
            return;
        }

        ConfigureLineRenderer();

        Vector3 center = CenterPosition;
        int pointCount = Mathf.Max(8, segments);
        lineRenderer.positionCount = pointCount;

        for (int i = 0; i < pointCount; i++)
        {
            float angle = i / (float)pointCount * Mathf.PI * 2f;
            Vector3 point = new Vector3(
                center.x + Mathf.Cos(angle) * radius,
                center.y + Mathf.Sin(angle) * radius,
                center.z);
            lineRenderer.SetPosition(i, point);
        }

        circleDirty = false;
    }
}
