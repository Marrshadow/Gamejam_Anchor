using System.Collections.Generic;
using UnityEngine;

public class AnchorFreezeZone : MonoBehaviour
{
    [SerializeField] private float freezeRadius = 2.4f;
    [SerializeField] private LayerMask freezableMask = ~0;
    [SerializeField] private Transform ignoredRoot;
    [SerializeField] private Vector3 circleCenterPosition;

    private readonly HashSet<IAnchorFreezable> frozenTargets = new HashSet<IAnchorFreezable>();
    private float anchorRadius = 0.18f;
    private bool isActive;

    public Vector3 CircleCenterPosition => circleCenterPosition;

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            circleCenterPosition = transform.position;
        }
    }

    public void Configure(float anchorRadius, float radius, LayerMask mask, Transform ignored)
    {
        this.anchorRadius = anchorRadius;
        freezeRadius = radius;
        freezableMask = mask;
        ignoredRoot = ignored;
        ApplyScale();
        SetCenterPosition(transform.position);

        CircleCollider2D trigger = GetComponent<CircleCollider2D>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
            trigger.radius = 0.5f;
        }
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
        if (!isActive)
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
            if (frozenTargets.Add(target))
            {
                target.SetFrozen(true);
            }
        }

        List<IAnchorFreezable> releasedTargets = new List<IAnchorFreezable>();
        foreach (IAnchorFreezable target in frozenTargets)
        {
            if (!currentTargets.Contains(target))
            {
                target.SetFrozen(false);
                releasedTargets.Add(target);
            }
        }

        for (int i = 0; i < releasedTargets.Count; i++)
        {
            frozenTargets.Remove(releasedTargets[i]);
        }
    }

    private bool IsIgnored(Transform candidate)
    {
        return ignoredRoot != null && (candidate == ignoredRoot || candidate.IsChildOf(ignoredRoot));
    }

    private void OnDisable()
    {
        ReleaseAll();
    }

    private void OnDestroy()
    {
        ReleaseAll();
    }

    private void ReleaseAll()
    {
        foreach (IAnchorFreezable target in frozenTargets)
        {
            target.SetFrozen(false);
        }

        frozenTargets.Clear();
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
}
