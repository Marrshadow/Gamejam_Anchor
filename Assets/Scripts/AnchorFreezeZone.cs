using System.Collections.Generic;
using UnityEngine;

public class AnchorFreezeZone : MonoBehaviour
{
    [SerializeField] private float freezeRadius = 2.4f;
    [SerializeField] private LayerMask freezableMask = ~0;
    [SerializeField] private Transform ignoredRoot;

    private readonly HashSet<IAnchorFreezable> frozenTargets = new HashSet<IAnchorFreezable>();
    private bool isActive;

    public void Configure(float anchorRadius, float radius, Color color, LayerMask mask, Transform ignored)
    {
        freezeRadius = radius;
        freezableMask = mask;
        ignoredRoot = ignored;
        transform.localScale = Vector3.one * anchorRadius * 2f;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = color;
        }

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

    public void SetActive(bool active)
    {
        if (isActive == active)
        {
            return;
        }

        isActive = active;
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

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, freezeRadius, freezableMask);
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

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, freezeRadius);
    }
}
