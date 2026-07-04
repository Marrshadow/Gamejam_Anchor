using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum AnchorFreezeMode
{
    FreezeInside,
    MoveInside
}

public class AnchorFreezeZone : MonoBehaviour
{
    [Header("Anchor")]
    [SerializeField] private Transform owner;
    [SerializeField] private float flightSpeed = 18f;
    [SerializeField] private float anchorRadius = 0.18f;

    [Header("Freeze")]
    [SerializeField] private float freezeRadius = 2.4f;
    [SerializeField] private float minFreezeRadius = 0.8f;
    [SerializeField] private float maxFreezeRadius = 5f;
    [SerializeField] private bool allowScrollResize = true;
    [SerializeField] private float scrollRadiusStep = 0.25f;
    [SerializeField] private LayerMask freezableMask = ~0;
    [SerializeField] private Transform ignoredRoot;
    [SerializeField] private Vector3 circleCenterPosition;
    [SerializeField] private AnchorFreezeMode freezeMode = AnchorFreezeMode.FreezeInside;

    [Header("Startup Freeze")]
    [SerializeField] private bool enableStartupShrink = true;
    [SerializeField] private float startupFreezeRadius = 100f;
    [SerializeField] private float startupShrinkDuration = 0.08f;

    [Header("Click Residue")]
    [SerializeField] private bool enableClickResidue = true;
    [SerializeField] private float residueFadeDuration = 1.2f;

    private static AnchorFreezeMode currentFreezeMode = AnchorFreezeMode.FreezeInside;
    private static readonly Dictionary<IAnchorFreezable, int> anchorEffectCounts = new Dictionary<IAnchorFreezable, int>();
    private static readonly HashSet<IAnchorFreezable> registeredTargets = new HashSet<IAnchorFreezable>();
    private readonly HashSet<IAnchorFreezable> affectedTargets = new HashSet<IAnchorFreezable>();
    private const float MouseWheelNotch = 120f;
    private Vector3 targetPosition;
    private Vector2 mouseBoundScreenPosition;
    private bool isFlying;
    private bool isAnchored;
    private bool isMouseBound = true;
    private bool isActive;
    private bool configured;
    private bool startupShrinking;
    private float startupShrinkElapsed;
    private float startupNormalFreezeRadius;
    private GameObject activeResidue;

    public Vector3 CircleCenterPosition => circleCenterPosition;
    public bool IsMouseBound => isMouseBound;
    public AnchorFreezeMode FreezeMode
    {
        get => freezeMode;
        set
        {
            freezeMode = value;
            ApplyGlobalFreezeMode(freezeMode);
        }
    }

    public bool EnableClickResidue
    {
        get => enableClickResidue;
        set => enableClickResidue = value;
    }

    private void OnValidate()
    {
        anchorRadius = Mathf.Max(0.01f, anchorRadius);
        flightSpeed = Mathf.Max(0.01f, flightSpeed);
        minFreezeRadius = Mathf.Max(0.01f, minFreezeRadius);
        maxFreezeRadius = Mathf.Max(minFreezeRadius, maxFreezeRadius);
        freezeRadius = Mathf.Clamp(freezeRadius, minFreezeRadius, maxFreezeRadius);
        scrollRadiusStep = Mathf.Max(0.01f, scrollRadiusStep);
        startupFreezeRadius = Mathf.Max(maxFreezeRadius, startupFreezeRadius);
        startupShrinkDuration = Mathf.Max(0.001f, startupShrinkDuration);
        residueFadeDuration = Mathf.Max(0.01f, residueFadeDuration);

        if (!Application.isPlaying)
        {
            circleCenterPosition = transform.position;
        }

        ApplyScale();

        if (Application.isPlaying)
        {
            ApplyGlobalFreezeMode(freezeMode);
        }
    }

    private void Awake()
    {
        ConfigureCollider();
        SetCenterPosition(transform.position);
        ApplyScale();
        ApplyGlobalFreezeMode(freezeMode);
    }

    private void Start()
    {
        ApplyGlobalFreezeMode(freezeMode);
        BeginStartupShrink();
    }

    private void OnEnable()
    {
        ApplyGlobalFreezeMode(freezeMode);
    }

    public void Configure(float anchorRadius, float radius, LayerMask mask, Transform ignored)
    {
        this.anchorRadius = anchorRadius;
        if (!Application.isPlaying || !configured)
        {
            SetFreezeRadius(radius);
        }
        else
        {
            SetFreezeRadius(freezeRadius);
        }

        freezableMask = mask;
        // 暂定玩家也受锚点效果影响
        // ignoredRoot = ignored;
        configured = true;
        ApplyScale();
        SetCenterPosition(transform.position);
        ConfigureCollider();
        ApplyGlobalFreezeMode(freezeMode);
    }

    public static void RegisterFreezable(IAnchorFreezable target)
    {
        if (IsMissingTarget(target))
        {
            return;
        }

        registeredTargets.Add(target);
        ApplyTargetFrozenState(target);
    }

    public static void UnregisterFreezable(IAnchorFreezable target)
    {
        if (IsMissingTarget(target))
        {
            return;
        }

        registeredTargets.Remove(target);
        target.SetFrozen(false);
    }

    private void ConfigureCollider()
    {
        CircleCollider2D trigger = GetComponent<CircleCollider2D>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
            trigger.radius = 0.5f;
        }
    }

    public void BindOwner(Transform nextOwner)
    {
        owner = nextOwner;
    }

    public void BindIgnoredRoot(Transform ignored)
    {
        ignoredRoot = ignored;
    }

    /// <summary> 
    /// 更新圆心坐标和位置
    /// </summary>
    /// <param name="worldPosition"></param>
    public void SetCenterPosition(Vector3 worldPosition)
    {
        circleCenterPosition = worldPosition;
        transform.position = circleCenterPosition;
    }

    public void SetActive(bool active)
    {
        if (isActive == active)
        {
            return;
        }

        isActive = active;
        ApplyScale();
        if (!isActive)
        {
            ReleaseAll();
        }
    }

    private void Update()
    {
        UpdateAnchorMovement();
        HandleClickResidue();

        if (!isActive)
        {
            return;
        }

        HandleRadiusScroll();

        Collider2D[] hits = Physics2D.OverlapCircleAll(circleCenterPosition, freezeRadius, freezableMask);
        HashSet<IAnchorFreezable> currentTargets = new HashSet<IAnchorFreezable>();

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null || IsIgnored(hits[i].transform))
            {
                continue;
            }

            IAnchorFreezable target = hits[i].GetComponentInParent<IAnchorFreezable>();
            if (target == null)
            {
                continue;
            }

            currentTargets.Add(target);
            if (affectedTargets.Add(target))
            {
                AcquireAnchorEffect(target);
            }
        }

        List<IAnchorFreezable> releasedTargets = new List<IAnchorFreezable>();
        foreach (IAnchorFreezable target in affectedTargets)
        {
            if (!currentTargets.Contains(target))
            {
                ReleaseAnchorEffect(target);
                releasedTargets.Add(target);
            }
        }

        for (int i = 0; i < releasedTargets.Count; i++)
        {
            affectedTargets.Remove(releasedTargets[i]);
        }

        UpdateStartupShrink();
    }

    private void HandleClickResidue()
    {
        if (!enableClickResidue || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        CreateResidue();
    }

    public void LaunchToScreenPosition(Vector2 screenPosition)
    {
        if (Camera.main == null)
        {
            return;
        }

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(screenPosition);
        mouseWorld.z = 0f;
        LaunchToWorldPosition(mouseWorld);
    }

    public void LaunchToWorldPosition(Vector3 worldPosition)
    {
        gameObject.SetActive(true);
        SetActive(false);
        SetCenterPosition(GetOwnerPosition());
        targetPosition = worldPosition;
        isMouseBound = false;
        isFlying = true;
        isAnchored = false;
    }

    public void BindAnchorToMouse(Vector2 screenPosition)
    {
        if (Camera.main == null)
        {
            return;
        }

        mouseBoundScreenPosition = screenPosition;
        gameObject.SetActive(true);
        SetActive(true);
        isMouseBound = true;
        isFlying = false;
        isAnchored = false;
        UpdateMouseBoundAnchor();
    }

    public void RetractAnchor()
    {
        isFlying = false;
        isAnchored = false;
        isMouseBound = false;
        SetActive(false);
        SetCenterPosition(GetOwnerPosition());
        gameObject.SetActive(false);
    }

    private void UpdateAnchorMovement()
    {
        if (isMouseBound)
        {
            UpdateMouseBoundAnchor();
            return;
        }

        if (isFlying)
        {
            MoveAnchor();
            return;
        }

        if (!isAnchored)
        {
            SetCenterPosition(GetOwnerPosition());
        }
    }

    private void MoveAnchor()
    {
        Vector3 nextPosition = Vector3.MoveTowards(
            circleCenterPosition,
            targetPosition,
            flightSpeed * Time.deltaTime);

        SetCenterPosition(nextPosition);

        if (Vector3.Distance(circleCenterPosition, targetPosition) > 0.01f)
        {
            return;
        }

        SetCenterPosition(targetPosition);
        SetActive(true);
        isFlying = false;
        isMouseBound = false;
        isAnchored = true;
    }

    private void UpdateMouseBoundAnchor()
    {
        if (Camera.main == null)
        {
            return;
        }

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mouseBoundScreenPosition);
        mouseWorld.z = 0f;
        SetCenterPosition(mouseWorld);
    }

    private Vector3 GetOwnerPosition()
    {
        return owner != null ? owner.position : transform.position;
    }

    private bool IsIgnored(Transform candidate)
    {
        return ignoredRoot != null && (candidate == ignoredRoot || candidate.IsChildOf(ignoredRoot));
    }

    private void HandleRadiusScroll()
    {
        if (startupShrinking || !allowScrollResize || Mouse.current == null)
        {
            return;
        }

        float scrollY = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scrollY) <= 0.01f)
        {
            return;
        }

        float radiusDelta = scrollY / MouseWheelNotch * scrollRadiusStep;
        SetFreezeRadius(freezeRadius + radiusDelta);
    }

    public void SetFreezeRadius(float radius)
    {
        freezeRadius = Mathf.Clamp(radius, minFreezeRadius, maxFreezeRadius);
        ApplyScale();
    }

    private void BeginStartupShrink()
    {
        if (!enableStartupShrink || freezeMode != AnchorFreezeMode.MoveInside)
        {
            return;
        }

        startupNormalFreezeRadius = Mathf.Clamp(freezeRadius, minFreezeRadius, maxFreezeRadius);
        startupShrinkElapsed = 0f;
        startupShrinking = true;
        SetActive(true);
        SetRawFreezeRadius(startupFreezeRadius);
    }

    private void UpdateStartupShrink()
    {
        if (!startupShrinking)
        {
            return;
        }

        startupShrinkElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(startupShrinkElapsed / startupShrinkDuration);
        SetRawFreezeRadius(Mathf.Lerp(startupFreezeRadius, startupNormalFreezeRadius, progress));

        if (progress >= 1f)
        {
            startupShrinking = false;
            SetFreezeRadius(startupNormalFreezeRadius);
        }
    }

    private void SetRawFreezeRadius(float radius)
    {
        freezeRadius = Mathf.Max(0.01f, radius);
        ApplyScale();
    }

    private void CreateResidue()
    {
        ClearResidue();

        activeResidue = new GameObject("AnchorFreezeZoneResidue");
        activeResidue.transform.position = transform.position;
        activeResidue.transform.rotation = transform.rotation;
        activeResidue.transform.localScale = transform.localScale;

        float residueRadius = GetCurrentVisualRadius();
        SpriteRenderer sourceRenderer = GetComponent<SpriteRenderer>();
        if (sourceRenderer != null)
        {
            SpriteRenderer residueRenderer = activeResidue.AddComponent<SpriteRenderer>();
            residueRenderer.sprite = sourceRenderer.sprite;
            residueRenderer.color = sourceRenderer.color;
            residueRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            residueRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            residueRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
            residueRenderer.flipX = sourceRenderer.flipX;
            residueRenderer.flipY = sourceRenderer.flipY;
            residueRenderer.drawMode = sourceRenderer.drawMode;
            residueRenderer.size = sourceRenderer.size;
        }

        activeResidue.AddComponent<ResidueFreezeZone>().Begin(
            transform.position,
            residueRadius,
            freezableMask,
            ignoredRoot);
        activeResidue.AddComponent<ResidueFader>().Begin(residueFadeDuration);
    }

    private float GetCurrentVisualRadius()
    {
        Vector3 scale = transform.lossyScale;
        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)) * 0.5f;
    }

    private void ClearResidue()
    {
        if (activeResidue != null)
        {
            Destroy(activeResidue);
            activeResidue = null;
        }
    }

    private void OnDisable()
    {
        ReleaseAll();
    }

    private void OnDestroy()
    {
        ReleaseAll();
        ClearResidue();
    }

    private void ReleaseAll()
    {
        foreach (IAnchorFreezable target in affectedTargets)
        {
            ReleaseAnchorEffect(target);
        }

        affectedTargets.Clear();
    }

    private static void AcquireAnchorEffect(IAnchorFreezable target)
    {
        if (IsMissingTarget(target))
        {
            return;
        }

        RegisterFreezable(target);

        if (!anchorEffectCounts.TryGetValue(target, out int count))
        {
            anchorEffectCounts.Add(target, 1);
            ApplyTargetFrozenState(target);
            return;
        }

        anchorEffectCounts[target] = count + 1;
        ApplyTargetFrozenState(target);
    }

    private static void ReleaseAnchorEffect(IAnchorFreezable target)
    {
        if (IsMissingTarget(target))
        {
            anchorEffectCounts.Remove(target);
            registeredTargets.Remove(target);
            return;
        }

        if (!anchorEffectCounts.TryGetValue(target, out int count))
        {
            ApplyTargetFrozenState(target);
            return;
        }

        count--;
        if (count <= 0)
        {
            anchorEffectCounts.Remove(target);
            ApplyTargetFrozenState(target);
            return;
        }

        anchorEffectCounts[target] = count;
        ApplyTargetFrozenState(target);
    }

    private static void ApplyGlobalFreezeMode(AnchorFreezeMode mode)
    {
        currentFreezeMode = mode;

        foreach (IAnchorFreezable target in registeredTargets)
        {
            ApplyTargetFrozenState(target);
        }
    }

    private static void ApplyTargetFrozenState(IAnchorFreezable target)
    {
        if (IsMissingTarget(target))
        {
            return;
        }

        bool affectedByAnchor = anchorEffectCounts.ContainsKey(target);
        bool shouldFreeze = currentFreezeMode == AnchorFreezeMode.FreezeInside
            ? affectedByAnchor
            : !affectedByAnchor;

        target.SetFrozen(shouldFreeze);
    }

    private static bool IsMissingTarget(IAnchorFreezable target)
    {
        if (target == null)
        {
            return true;
        }

        Object unityObject = target as Object;
        return unityObject == null;
    }

    private void ApplyScale()
    {
        float radius = isActive ? freezeRadius : anchorRadius;
        transform.localScale = Vector3.one * radius * 2f;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.35f);
        Vector3 center = Application.isPlaying ? circleCenterPosition : transform.position;
        Gizmos.DrawWireSphere(center, freezeRadius);
    }

    private class ResidueFader : MonoBehaviour
    {
        private SpriteRenderer spriteRenderer;
        private Color startColor;
        private float duration = 1f;
        private float elapsed;

        public void Begin(float fadeDuration)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
            duration = Mathf.Max(0.01f, fadeDuration);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (spriteRenderer != null)
            {
                Color nextColor = startColor;
                nextColor.a = Mathf.Lerp(startColor.a, 0f, elapsed / duration);
                spriteRenderer.color = nextColor;
            }

            if (elapsed >= duration)
            {
                Destroy(gameObject);
            }
        }
    }

    private class ResidueFreezeZone : MonoBehaviour
    {
        private readonly HashSet<IAnchorFreezable> affectedTargets = new HashSet<IAnchorFreezable>();
        private Vector3 center;
        private float radius;
        private LayerMask freezableMask;
        private Transform ignoredRoot;

        public void Begin(Vector3 freezeCenter, float freezeRadius, LayerMask mask, Transform ignored)
        {
            center = freezeCenter;
            radius = freezeRadius;
            freezableMask = mask;
            ignoredRoot = ignored;
        }

        private void Update()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, freezableMask);
            HashSet<IAnchorFreezable> currentTargets = new HashSet<IAnchorFreezable>();

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == null || IsIgnored(hits[i].transform))
                {
                    continue;
                }

                IAnchorFreezable target = hits[i].GetComponentInParent<IAnchorFreezable>();
                if (target == null)
                {
                    continue;
                }

                currentTargets.Add(target);
                if (affectedTargets.Add(target))
                {
                    AcquireAnchorEffect(target);
                }
            }

            List<IAnchorFreezable> releasedTargets = new List<IAnchorFreezable>();
            foreach (IAnchorFreezable target in affectedTargets)
            {
                if (!currentTargets.Contains(target))
                {
                    ReleaseAnchorEffect(target);
                    releasedTargets.Add(target);
                }
            }

            for (int i = 0; i < releasedTargets.Count; i++)
            {
                affectedTargets.Remove(releasedTargets[i]);
            }
        }

        private bool IsIgnored(Transform candidate)
        {
            return ignoredRoot != null && (candidate == ignoredRoot || candidate.IsChildOf(ignoredRoot));
        }

        private void OnDestroy()
        {
            foreach (IAnchorFreezable target in affectedTargets)
            {
                ReleaseAnchorEffect(target);
            }

            affectedTargets.Clear();
        }
    }
}
