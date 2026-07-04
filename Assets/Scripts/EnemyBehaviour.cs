using SKCell;
using UnityEngine;

public class EnemyBehaviour : MonoBehaviour, IAnchorFreezable
{
    private SKPathDesigner pathDesigner;
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    [SerializeField] private bool spriteFacesRight;
    [SerializeField] private float turnThreshold = 0.001f;

    private bool frozen;
    private Vector3 lastPosition;
    private float animatorSpeed = 1f;

    private void Reset()
    {
        pathDesigner = GetComponent<SKPathDesigner>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (pathDesigner == null)
        {
            pathDesigner = GetComponent<SKPathDesigner>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animatorSpeed = animator.speed;
        }

        lastPosition = transform.position;
    }

    private void LateUpdate()
    {
        Vector3 currentPosition = transform.position;
        float horizontalDelta = currentPosition.x - lastPosition.x;

        if (!frozen && Mathf.Abs(horizontalDelta) > turnThreshold)
        {
            ApplyFacing(horizontalDelta);
        }

        lastPosition = currentPosition;
    }

    public void SetFrozen(bool freeze)
    {
        if (frozen == freeze)
        {
            return;
        }

        frozen = freeze;

        if (pathDesigner != null)
        {
            if (freeze)
            {
                pathDesigner.PausePath();
            }
            else
            {
                pathDesigner.ResumePath();
            }
        }

        if (animator != null)
        {
            if (freeze)
            {
                animatorSpeed = animator.speed;
            }

            animator.speed = freeze ? 0f : animatorSpeed;
        }

        lastPosition = transform.position;
    }

    private void ApplyFacing(float horizontalDelta)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        bool movingRight = horizontalDelta > 0f;
        spriteRenderer.flipX = movingRight != spriteFacesRight;
    }
}
