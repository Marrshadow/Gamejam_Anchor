using UnityEngine;

public class BlinkingPlatform2D : MonoBehaviour, IAnchorFreezable
{
    [SerializeField] private float visibleDuration = 2f;
    [SerializeField] private float hiddenDuration = 1.25f;
    [SerializeField] private bool startsVisible = true;

    private SpriteRenderer spriteRenderer;
    private Collider2D platformCollider;
    private float timer;
    private bool visible;
    private bool frozen;

    public void Configure(float showTime, float hideTime, bool initiallyVisible)
    {
        visibleDuration = showTime;
        hiddenDuration = hideTime;
        startsVisible = initiallyVisible;
        ApplyState(startsVisible);
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        platformCollider = GetComponent<Collider2D>();
        visible = startsVisible;
        ApplyState(visible);
    }

    private void Update()
    {
        if (frozen)
        {
            return;
        }

        timer += Time.deltaTime;
        float currentDuration = visible ? visibleDuration : hiddenDuration;
        if (timer < currentDuration)
        {
            return;
        }

        timer = 0f;
        ApplyState(!visible);
    }

    public void SetFrozen(bool freeze)
    {
        frozen = freeze;
    }

    private void ApplyState(bool nextVisible)
    {
        visible = nextVisible;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = visible;
        }

        if (platformCollider != null)
        {
            platformCollider.enabled = visible;
        }
    }
}
