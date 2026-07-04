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
    [SerializeField] private Transform anchorCenter;
    [SerializeField] private MouseRangeCircle2D mouseRangeCircle;
    [SerializeField] private float flightSpeed = 18f;
    [SerializeField] private float anchorRadius = 0.18f;

    [Header("Freeze")]
    [SerializeField] private float freezeRadius = 2.4f;
    [SerializeField] private float minFreezeRadius = 0.8f;
    [SerializeField] private float maxFreezeRadius = 5f;
    [SerializeField] private bool allowScrollResize = true;
    [SerializeField] private float scrollRadiusStep = 0.25f;
    [SerializeField] private bool resizeMouseRangeWithFreeze = true;
    [SerializeField] private float mouseRangeGrowthPerFreezeUnit = 1f;
    [SerializeField] private float minMouseRangeRadius = 0.1f;
    [SerializeField] private LayerMask freezableMask = ~0;
    [SerializeField] private Transform ignoredRoot;
    [SerializeField] private Vector3 circleCenterPosition;
    [SerializeField] private AnchorFreezeMode freezeMode = AnchorFreezeMode.FreezeInside;

    [Header("Startup Freeze")]
    [SerializeField] private bool enableStartupShrink = true;
    [SerializeField] private float startupShrinkDuration = 0.08f;

    [Header("Startup Global Pulse")]
    [SerializeField] private float startupGlobalPulseRadius = 120f;

    [Header("Click Residue")]
    [SerializeField] private bool enableClickResidue = true;
    [SerializeField] private float residueFadeDuration = 1.2f;

    private static AnchorFreezeMode currentFreezeMode = AnchorFreezeMode.FreezeInside;
    private static readonly Dictionary<IAnchorFreezable, int> anchorEffectCounts = new Dictionary<IAnchorFreezable, int>();
    private static readonly Dictionary<IAnchorFreezable, int> startupCameraEffectCounts = new Dictionary<IAnchorFreezable, int>();
    private static readonly HashSet<IAnchorFreezable> registeredTargets = new HashSet<IAnchorFreezable>();
    private static readonly HashSet<AnchorFreezeZone> activeZones = new HashSet<AnchorFreezeZone>();
    private static HashSet<IAnchorFreezable> freezeApplyFilter;
    private static bool refreshingActiveZones;
    private readonly HashSet<IAnchorFreezable> affectedTargets = new HashSet<IAnchorFreezable>();
    private readonly HashSet<IAnchorFreezable> startupCameraTargets = new HashSet<IAnchorFreezable>();
    private const float MouseWheelNotch = 120f;
    private const float CameraBoundsDepth = 100000f;
    private Vector3 targetPosition;
    private Vector2 mouseBoundScreenPosition;
    private bool isFlying;
    private bool isAnchored;
    private bool isMouseBound = true;
    private bool isActive;
    private bool configured;
    private bool startupShrinking;
    private bool modeSwitchPulsing;
    private bool freezeModeStateInitialized;
    private bool mouseRangeRadiusInitialized;
    private float baseFreezeRadiusForMouseRange;
    private float baseMouseRangeRadius;
    private float modeSwitchPulseElapsed;
    private float modeSwitchPulseStartRadius;
    private float modeSwitchPulseTargetRadius;
    private float modeSwitchPulseCurrentDuration;
    private bool modeSwitchPulseIsStartup;
    private AnchorFreezeMode appliedFreezeMode;
    private GameObject activeResidue;
    private Rigidbody2D anchorCenterBody;
    private Collider2D[] anchorCenterColliders;
    private const float AnchorCenterSyncSqrTolerance = 0.000001f;
    private const string AnchorCentreName = "AnchorCentre";
    private const string AnchorCenterName = "AnchorCenter";

    public Vector3 CircleCenterPosition => circleCenterPosition;
    public bool IsMouseBound => isMouseBound;
    public AnchorFreezeMode FreezeMode
    {
        get => freezeMode;
        set
        {
            freezeMode = value;
            ApplyFreezeModeState();
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
        mouseRangeGrowthPerFreezeUnit = Mathf.Max(0f, mouseRangeGrowthPerFreezeUnit);
        minMouseRangeRadius = Mathf.Max(0.01f, minMouseRangeRadius);
        startupShrinkDuration = Mathf.Max(0.001f, startupShrinkDuration);
        startupGlobalPulseRadius = Mathf.Max(maxFreezeRadius, startupGlobalPulseRadius);
        residueFadeDuration = Mathf.Max(0.01f, residueFadeDuration);

        if (!Application.isPlaying)
        {
            circleCenterPosition = transform.position;
        }

        ApplyScale();

        if (Application.isPlaying)
        {
            ConfigureAnchorCenterPhysics();
            ApplyFreezeModeState();
        }
    }

    private void Awake()
    {
        ResolveAnchorCenter();
        ResolveMouseRangeCircle();
        ConfigureAnchorCenterPhysics();
        ConfigureCollider();
        SetCenterPosition(GetAnchorCenterPosition());
        ApplyScale();
        ApplyFreezeModeState();
    }

    private void LateUpdate()
    {
        SyncZoneToAnchorCenter();
    }

    private void Start()
    {
        ApplyFreezeModeState();
    }

    private void OnEnable()
    {
        activeZones.Add(this);
        ResolveAnchorCenter();
        ResolveMouseRangeCircle();
        ConfigureAnchorCenterPhysics();
        ApplyFreezeModeState();
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
        ResolveAnchorCenter();
        SetCenterPosition(GetAnchorCenterPosition());
        ConfigureAnchorCenterPhysics();
        ConfigureCollider();
        ApplyFreezeModeState();
    }

    public static void RegisterFreezable(IAnchorFreezable target)
    {
        if (IsMissingTarget(target))
        {
            return;
        }

        registeredTargets.Add(target);
        RefreshActiveZones();
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
        ResolveMouseRangeCircle();
        ConfigureAnchorCenterPhysics();
    }

    public void BindMouseRangeCircle(MouseRangeCircle2D rangeCircle)
    {
        mouseRangeCircle = rangeCircle;
        CaptureMouseRangeBaseline();
    }

    public void BindIgnoredRoot(Transform ignored)
    {
        ignoredRoot = ignored;
        ConfigureAnchorCenterPhysics();
    }

    /// <summary> 
    /// 更新圆心坐标和位置
    /// </summary>
    /// <param name="worldPosition"></param>
    public void SetCenterPosition(Vector3 worldPosition)
    {
        if (anchorCenter != null)
        {
            MoveAnchorCenterTo(worldPosition);
            SyncZoneToAnchorCenter();
            RefreshAffectedTargets();
            return;
        }

        circleCenterPosition = worldPosition;
        transform.position = circleCenterPosition;
        RefreshAffectedTargets();
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
            ApplyGlobalFreezeMode(freezeMode);
            return;
        }

        RefreshAffectedTargets();
        ApplyGlobalFreezeMode(freezeMode);
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
        UpdateModeSwitchPulse();

        RefreshAffectedTargets();
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

        LaunchToWorldPosition(GetMouseWorldPosition(screenPosition));
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

    public void ToggleFreezeMode()
    {
        FreezeMode = freezeMode == AnchorFreezeMode.FreezeInside
            ? AnchorFreezeMode.MoveInside
            : AnchorFreezeMode.FreezeInside;
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

        if (Mouse.current != null)
        {
            mouseBoundScreenPosition = Mouse.current.position.ReadValue();
        }

        SetCenterPosition(GetMouseWorldPosition(mouseBoundScreenPosition));
    }

    private Vector3 GetMouseWorldPosition(Vector2 screenPosition)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return circleCenterPosition;
        }

        ResolveMouseRangeCircle();
        if (mouseRangeCircle != null)
        {
            return mouseRangeCircle.ScreenToClampedWorld(screenPosition, mainCamera);
        }

        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(screenPosition);
        mouseWorld.z = 0f;
        return mouseWorld;
    }

    private void ResolveMouseRangeCircle()
    {
        PlayerController player = owner != null
            ? owner.GetComponentInParent<PlayerController>()
            : null;

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
        }

        if (owner == null && player != null)
        {
            owner = player.transform;
        }

        if (mouseRangeCircle == null && owner != null)
        {
            mouseRangeCircle = owner.GetComponentInChildren<MouseRangeCircle2D>(true);
        }

        if (mouseRangeCircle == null)
        {
            mouseRangeCircle = FindFirstObjectByType<MouseRangeCircle2D>();
        }

        if (mouseRangeCircle != null && player != null)
        {
            mouseRangeCircle.BindPlayer(player.transform);
        }

        CaptureMouseRangeBaseline();
    }

    private void CaptureMouseRangeBaseline()
    {
        if (mouseRangeRadiusInitialized || mouseRangeCircle == null)
        {
            return;
        }

        baseFreezeRadiusForMouseRange = freezeRadius;
        baseMouseRangeRadius = mouseRangeCircle.Radius;
        mouseRangeRadiusInitialized = true;
    }

    private void ApplyMouseRangeRadiusForCurrentFreeze()
    {
        if (!allowScrollResize || !resizeMouseRangeWithFreeze)
        {
            return;
        }

        ResolveMouseRangeCircle();
        if (!mouseRangeRadiusInitialized || mouseRangeCircle == null)
        {
            return;
        }

        float freezeShrinkAmount = baseFreezeRadiusForMouseRange - freezeRadius;
        float nextMouseRangeRadius = baseMouseRangeRadius + freezeShrinkAmount * mouseRangeGrowthPerFreezeUnit;
        mouseRangeCircle.Radius = Mathf.Max(minMouseRangeRadius, nextMouseRangeRadius);
    }

    private Vector3 GetOwnerPosition()
    {
        return owner != null ? owner.position : transform.position;
    }

    private Vector3 GetAnchorCenterPosition()
    {
        ResolveAnchorCenter();
        return anchorCenter != null ? anchorCenter.position : transform.position;
    }

    private void ResolveAnchorCenter()
    {
        if (anchorCenter != null)
        {
            if (anchorCenterBody == null)
            {
                anchorCenterBody = anchorCenter.GetComponent<Rigidbody2D>();
            }

            return;
        }

        Transform centre = transform.Find(AnchorCentreName);
        if (centre == null)
        {
            centre = transform.Find(AnchorCenterName);
        }

        if (centre == null)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] == transform)
                {
                    continue;
                }

                if (children[i].name == AnchorCentreName || children[i].name == AnchorCenterName)
                {
                    centre = children[i];
                    break;
                }
            }
        }

        anchorCenter = centre;
        anchorCenterBody = anchorCenter != null ? anchorCenter.GetComponent<Rigidbody2D>() : null;
        ConfigureAnchorCenterPhysics();
    }

    private void MoveAnchorCenterTo(Vector3 worldPosition)
    {
        ResolveAnchorCenter();
        if (anchorCenter == null)
        {
            return;
        }

        if (anchorCenterBody != null && Application.isPlaying)
        {
            KeepAnchorCenterUpright();
            Vector2 nextPosition = worldPosition;
            anchorCenterBody.MovePosition(nextPosition);
            anchorCenterBody.position = nextPosition;
            anchorCenter.position = nextPosition;
            return;
        }

        anchorCenter.position = worldPosition;
    }

    private void ConfigureAnchorCenterPhysics()
    {
        if (anchorCenter == null)
        {
            return;
        }

        if (anchorCenterBody == null)
        {
            anchorCenterBody = anchorCenter.GetComponent<Rigidbody2D>();
        }

        if (anchorCenterBody != null)
        {
            anchorCenterBody.bodyType = RigidbodyType2D.Kinematic;
            anchorCenterBody.freezeRotation = true;
            anchorCenterBody.gravityScale = 0f;
            anchorCenterBody.linearVelocity = Vector2.zero;
            anchorCenterBody.angularVelocity = 0f;
            anchorCenterBody.rotation = 0f;
            anchorCenterBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            anchorCenterBody.interpolation = RigidbodyInterpolation2D.None;
        }

        CacheAnchorCenterColliders();
        RefreshAnchorCenterCollisionIgnores();
    }

    private void KeepAnchorCenterUpright()
    {
        if (anchorCenterBody != null)
        {
            anchorCenterBody.linearVelocity = Vector2.zero;
            anchorCenterBody.angularVelocity = 0f;
            anchorCenterBody.rotation = 0f;
        }

        if (anchorCenter != null)
        {
            anchorCenter.rotation = Quaternion.identity;
        }
    }

    private void CacheAnchorCenterColliders()
    {
        if (anchorCenter == null)
        {
            return;
        }

        if (anchorCenterColliders == null || anchorCenterColliders.Length == 0)
        {
            anchorCenterColliders = anchorCenter.GetComponentsInChildren<Collider2D>(true);
        }
    }

    private void RefreshAnchorCenterCollisionIgnores()
    {
        if (anchorCenter == null)
        {
            return;
        }

        CacheAnchorCenterColliders();
        if (anchorCenterColliders == null || anchorCenterColliders.Length == 0)
        {
            return;
        }

        Collider2D[] allColliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        for (int i = 0; i < anchorCenterColliders.Length; i++)
        {
            Collider2D anchorCollider = anchorCenterColliders[i];
            if (anchorCollider == null)
            {
                continue;
            }

            for (int j = 0; j < allColliders.Length; j++)
            {
                Collider2D otherCollider = allColliders[j];
                if (otherCollider == null || otherCollider == anchorCollider)
                {
                    continue;
                }

                if (otherCollider.transform == anchorCenter || otherCollider.transform.IsChildOf(anchorCenter))
                {
                    continue;
                }

                Physics2D.IgnoreCollision(anchorCollider, otherCollider, true);
            }
        }
    }

    private void SyncZoneToAnchorCenter()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveAnchorCenter();
        if (anchorCenter == null)
        {
            return;
        }

        Vector3 centerPosition = anchorCenter.position;
        KeepAnchorCenterUpright();

        if ((circleCenterPosition - centerPosition).sqrMagnitude <= AnchorCenterSyncSqrTolerance
            && (transform.position - centerPosition).sqrMagnitude <= AnchorCenterSyncSqrTolerance)
        {
            return;
        }

        circleCenterPosition = centerPosition;
        transform.position = centerPosition;
        anchorCenter.position = centerPosition;
        RefreshAffectedTargets();
    }

    private bool IsOwnerOrPlayer(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (owner != null && (candidate == owner || candidate.IsChildOf(owner)))
        {
            return true;
        }

        PlayerController player = candidate.GetComponentInParent<PlayerController>();
        return player != null && (owner == null || player.transform == owner || owner.IsChildOf(player.transform));
    }

    private bool IsIgnored(Transform candidate)
    {
        return ignoredRoot != null && (candidate == ignoredRoot || candidate.IsChildOf(ignoredRoot));
    }

    private void HandleRadiusScroll()
    {
        if (startupShrinking || modeSwitchPulsing || !allowScrollResize || Mouse.current == null)
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
        ApplyMouseRangeRadiusForCurrentFreeze();
    }

    public void SetFreezeRadius(float radius)
    {
        freezeRadius = Mathf.Clamp(radius, minFreezeRadius, maxFreezeRadius);
        ApplyScale();
        RefreshAffectedTargets();
        ApplyGlobalFreezeMode(freezeMode);
    }

    private void SetFreezeRadiusRaw(float radius)
    {
        freezeRadius = Mathf.Max(0.01f, radius);
        ApplyScale();
        RefreshAffectedTargets();
        ApplyGlobalFreezeMode(freezeMode);
    }

    private void BeginRadiusPulse(float duration, bool isStartupPulse)
    {
        bool wasPulsing = modeSwitchPulsing;
        gameObject.SetActive(true);
        SetActiveForModeSwitch(true);
        modeSwitchPulsing = true;
        modeSwitchPulseIsStartup = modeSwitchPulseIsStartup || isStartupPulse;
        modeSwitchPulseElapsed = 0f;
        modeSwitchPulseCurrentDuration = Mathf.Max(0.001f, duration);
        modeSwitchPulseStartRadius = Mathf.Max(startupGlobalPulseRadius, maxFreezeRadius);
        modeSwitchPulseTargetRadius = wasPulsing
            ? modeSwitchPulseTargetRadius
            : Mathf.Clamp(freezeRadius, minFreezeRadius, maxFreezeRadius);
        SetFreezeRadiusRaw(modeSwitchPulseStartRadius);
    }

    private void UpdateModeSwitchPulse()
    {
        if (!modeSwitchPulsing)
        {
            return;
        }

        modeSwitchPulseElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(modeSwitchPulseElapsed / modeSwitchPulseCurrentDuration);
        float easedProgress = 1f - (1f - progress) * (1f - progress);
        SetFreezeRadiusRaw(Mathf.Lerp(modeSwitchPulseStartRadius, modeSwitchPulseTargetRadius, easedProgress));

        if (progress < 1f)
        {
            return;
        }

        modeSwitchPulsing = false;
        if (modeSwitchPulseIsStartup)
        {
            startupShrinking = false;
        }

        modeSwitchPulseIsStartup = false;
        SetFreezeRadius(modeSwitchPulseTargetRadius);
        ApplyMouseRangeRadiusForCurrentFreeze();
    }

    private void BeginStartupShrink()
    {
        if (!enableStartupShrink || freezeMode != AnchorFreezeMode.FreezeInside)
        {
            return;
        }

        if (startupShrinking)
        {
            return;
        }

        startupShrinking = true;
        BeginRadiusPulse(startupShrinkDuration, true);
    }

    private void ApplyFreezeModeState()
    {
        bool modeChanged = freezeModeStateInitialized && appliedFreezeMode != freezeMode;
        freezeModeStateInitialized = true;
        appliedFreezeMode = freezeMode;

        if (freezeMode == AnchorFreezeMode.MoveInside)
        {
            startupShrinking = false;
            ReleaseStartupCameraFreeze(modeChanged);
            SetActiveForModeSwitch(true);
            ApplyFreezeModeChange(modeChanged);
            return;
        }

        ApplyFreezeModeChange(modeChanged);
        if (!modeChanged)
        {
            BeginStartupShrink();
        }
    }

    private void ApplyFreezeModeChange(bool cameraOnly)
    {
        if (cameraOnly)
        {
            ApplyCameraScopedFreezeMode(freezeMode);
            return;
        }

        ApplyGlobalFreezeMode(freezeMode);
    }

    private void SetActiveForModeSwitch(bool active)
    {
        if (isActive == active)
        {
            return;
        }

        isActive = active;
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
        ReleaseStartupCameraFreeze();
        ReleaseAll();
        activeZones.Remove(this);
        ApplyGlobalFreezeMode(freezeMode);
    }

    private void OnDestroy()
    {
        ReleaseStartupCameraFreeze();
        ReleaseAll();
        activeZones.Remove(this);
        ClearResidue();
    }

    private void AcquireStartupCameraFreeze()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Bounds cameraBounds = GetCameraWorldBounds(mainCamera);
        foreach (IAnchorFreezable target in registeredTargets)
        {
            if (IsMissingTarget(target) || !IsTargetVisibleInCamera(target, cameraBounds))
            {
                continue;
            }

            Component targetComponent = target as Component;
            if (targetComponent != null && IsIgnored(targetComponent.transform))
            {
                continue;
            }

            if (!startupCameraTargets.Add(target))
            {
                continue;
            }

            if (!startupCameraEffectCounts.TryGetValue(target, out int count))
            {
                startupCameraEffectCounts.Add(target, 1);
            }
            else
            {
                startupCameraEffectCounts[target] = count + 1;
            }
        }

        ApplyGlobalFreezeMode(freezeMode);
    }

    private void ReleaseStartupCameraFreeze()
    {
        ReleaseStartupCameraFreeze(false);
    }

    private void ReleaseStartupCameraFreeze(bool cameraOnly)
    {
        if (!startupShrinking && startupCameraTargets.Count == 0)
        {
            return;
        }

        CancelInvoke(nameof(ReleaseStartupCameraFreeze));
        startupShrinking = false;
        foreach (IAnchorFreezable target in startupCameraTargets)
        {
            if (!startupCameraEffectCounts.TryGetValue(target, out int count))
            {
                continue;
            }

            count--;
            if (count <= 0)
            {
                startupCameraEffectCounts.Remove(target);
            }
            else
            {
                startupCameraEffectCounts[target] = count;
            }
        }

        startupCameraTargets.Clear();
        if (cameraOnly)
        {
            ApplyCameraScopedFreezeMode(freezeMode);
            return;
        }

        ApplyGlobalFreezeMode(freezeMode);
    }

    private void RefreshAffectedTargets()
    {
        if (!Application.isPlaying || !isActive)
        {
            return;
        }

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
    }

    private static Bounds GetCameraWorldBounds(Camera camera)
    {
        if (camera.orthographic)
        {
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            Vector3 center = camera.transform.position;
            center.z = 0f;
            return new Bounds(center, new Vector3(halfWidth * 2f, halfHeight * 2f, CameraBoundsDepth));
        }

        float distanceToWorldPlane = Mathf.Abs(camera.transform.position.z);
        Vector3 bottomLeft = camera.ViewportToWorldPoint(new Vector3(0f, 0f, distanceToWorldPlane));
        Vector3 topRight = camera.ViewportToWorldPoint(new Vector3(1f, 1f, distanceToWorldPlane));
        Vector3 centerPoint = (bottomLeft + topRight) * 0.5f;
        centerPoint.z = 0f;
        return new Bounds(centerPoint, new Vector3(Mathf.Abs(topRight.x - bottomLeft.x), Mathf.Abs(topRight.y - bottomLeft.y), CameraBoundsDepth));
    }

    private static bool IsTargetVisibleInCamera(IAnchorFreezable target, Bounds cameraBounds)
    {
        if (!TryGetTargetBounds(target, out Bounds targetBounds))
        {
            return false;
        }

        return cameraBounds.Intersects(targetBounds);
    }

    private static bool TryGetTargetBounds(IAnchorFreezable target, out Bounds targetBounds)
    {
        targetBounds = default;
        Component component = target as Component;
        if (component == null)
        {
            return false;
        }

        bool hasBounds = false;
        Collider2D[] colliders = component.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            EncapsulateBounds(ref targetBounds, colliders[i].bounds, ref hasBounds);
        }

        Renderer[] renderers = component.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            EncapsulateBounds(ref targetBounds, renderers[i].bounds, ref hasBounds);
        }

        if (hasBounds)
        {
            return true;
        }

        targetBounds = new Bounds(component.transform.position, Vector3.zero);
        return true;
    }

    private static void EncapsulateBounds(ref Bounds aggregateBounds, Bounds nextBounds, ref bool hasBounds)
    {
        if (!hasBounds)
        {
            aggregateBounds = nextBounds;
            hasBounds = true;
            return;
        }

        aggregateBounds.Encapsulate(nextBounds);
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
        RefreshActiveZones();

        foreach (IAnchorFreezable target in registeredTargets)
        {
            ApplyTargetFrozenState(target);
        }
    }

    private static void ApplyCameraScopedFreezeMode(AnchorFreezeMode mode)
    {
        currentFreezeMode = mode;
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Bounds cameraBounds = GetCameraWorldBounds(mainCamera);
        HashSet<IAnchorFreezable> cameraTargets = new HashSet<IAnchorFreezable>();
        foreach (IAnchorFreezable target in registeredTargets)
        {
            if (IsMissingTarget(target) || !IsTargetVisibleInCamera(target, cameraBounds))
            {
                continue;
            }

            cameraTargets.Add(target);
        }

        try
        {
            freezeApplyFilter = cameraTargets;
            RefreshActiveZones();
            foreach (IAnchorFreezable target in cameraTargets)
            {
                ApplyTargetFrozenState(target);
            }
        }
        finally
        {
            freezeApplyFilter = null;
        }
    }

    private static void RefreshActiveZones()
    {
        if (refreshingActiveZones)
        {
            return;
        }

        refreshingActiveZones = true;
        List<AnchorFreezeZone> missingZones = new List<AnchorFreezeZone>();
        foreach (AnchorFreezeZone zone in activeZones)
        {
            if (zone == null)
            {
                missingZones.Add(zone);
                continue;
            }

            zone.RefreshAffectedTargets();
        }

        for (int i = 0; i < missingZones.Count; i++)
        {
            activeZones.Remove(missingZones[i]);
        }
        refreshingActiveZones = false;
    }

    private static void ApplyTargetFrozenState(IAnchorFreezable target)
    {
        if (IsMissingTarget(target))
        {
            return;
        }

        if (freezeApplyFilter != null && !freezeApplyFilter.Contains(target))
        {
            return;
        }

        bool affectedByAnchor = anchorEffectCounts.ContainsKey(target);
        if (currentFreezeMode == AnchorFreezeMode.FreezeInside && startupCameraEffectCounts.ContainsKey(target))
        {
            affectedByAnchor = true;
        }

        bool shouldFreeze = currentFreezeMode == AnchorFreezeMode.FreezeInside
            ? affectedByAnchor
            : !affectedByAnchor;

        target.SetFrozen(shouldFreeze);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        currentFreezeMode = AnchorFreezeMode.FreezeInside;
        anchorEffectCounts.Clear();
        startupCameraEffectCounts.Clear();
        registeredTargets.Clear();
        activeZones.Clear();
        freezeApplyFilter = null;
        refreshingActiveZones = false;
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
