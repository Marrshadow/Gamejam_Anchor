using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour, PlayerInputControl.IGamePlayActions, IAnchorFreezable
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float jumpForce = 13f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.16f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Actions")]
    [SerializeField] private AnchorFreezeZone anchor;

    private PlayerInputControl inputControl;
    private Rigidbody2D body;
    private Vector2 moveInput;
    private Vector2 pointerScreenPosition;
    private bool jumpQueued;
    private bool frozen;
    private float cachedGravityScale = 1f;
    private readonly Collider2D[] groundHits = new Collider2D[8];

    private void Awake()
    {
        inputControl = new PlayerInputControl();
        body = GetComponent<Rigidbody2D>();
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        cachedGravityScale = body.gravityScale;

        if (anchor == null)
        {
            anchor = GetComponentInChildren<AnchorFreezeZone>();
        }

        if (anchor != null)
        {
            anchor.BindOwner(transform);
        }
    }

    private void OnEnable()
    {
        inputControl.GamePlay.AddCallbacks(this);
        inputControl.GamePlay.Enable();
        AnchorFreezeZone.RegisterFreezable(this);
    }

    private void OnDisable()
    {
        AnchorFreezeZone.UnregisterFreezable(this);
        inputControl.GamePlay.RemoveCallbacks(this);
        inputControl.GamePlay.Disable();
    }

    private void OnDestroy()
    {
        inputControl.Dispose();
    }

    private void FixedUpdate()
    {
        if (frozen)
        {
            body.linearVelocity = Vector2.zero;
            jumpQueued = false;
            return;
        }

        Vector2 platformVelocity = GetStandingPlatformVelocity();
        Vector2 nextVelocity = body.linearVelocity;
        nextVelocity.x = moveInput.x * moveSpeed + platformVelocity.x;
        if (!jumpQueued && platformVelocity.y > nextVelocity.y)
        {
            nextVelocity.y = platformVelocity.y;
        }

        body.linearVelocity = nextVelocity;

        if (!jumpQueued)
        {
            return;
        }

        body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
        body.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        jumpQueued = false;
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (frozen)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = context.ReadValue<Vector2>();

        if (Mathf.Abs(moveInput.x) > 0.01f)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(moveInput.x);
            transform.localScale = scale;
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (frozen || !context.started)
        {
            return;
        }

        if (IsGrounded())
        {
            jumpQueued = true;
        }
    }

    public void OnFire(InputAction.CallbackContext context)
    {
    }

    public void OnMouse(InputAction.CallbackContext context)
    {
        pointerScreenPosition = context.ReadValue<Vector2>();

        if (anchor != null && anchor.IsMouseBound)
        {
            anchor.BindAnchorToMouse(pointerScreenPosition);
        }
    }

    public void OnLook(InputAction.CallbackContext context)
    {
    }

    public void OnDash(InputAction.CallbackContext context)
    {
    }

    public void OnPlaceMode1(InputAction.CallbackContext context)
    {
    }

    public void OnPlaceMode2(InputAction.CallbackContext context)
    {
    }

    /// <summary>
    /// 将鼠标和锚点绑定
    /// </summary>
    /// <param name="context"></param>
    public void OnConfirm(InputAction.CallbackContext context)
    {
        // 点击逻辑已移到 AnchorFreezeZone，避免 PlayerController 和 Zone 同时响应鼠标点击。
        // if (!context.started || anchor == null)
        // {
        //     return;
        // }
        //
        // anchor.BindAnchorToMouse(pointerScreenPosition);
    }

    public void OnCancle(InputAction.CallbackContext context)
    {
        // 点击逻辑已移到 AnchorFreezeZone，避免 PlayerController 和 Zone 同时响应鼠标点击。
        // if (!context.started || anchor == null)
        // {
        //     return;
        // }
        //
        // anchor.RetractAnchor();
    }

    public void SetFrozen(bool freeze)
    {
        if (frozen == freeze)
        {
            return;
        }

        frozen = freeze;
        moveInput = Vector2.zero;
        jumpQueued = false;

        if (body != null)
        {
            if (freeze)
            {
                cachedGravityScale = body.gravityScale;
            }

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.gravityScale = freeze ? 0f : cachedGravityScale;
        }
    }

    private bool IsGrounded()
    {
        Vector2 checkPosition = groundCheck != null
            ? groundCheck.position
            : transform.position + Vector3.down * 0.55f;

        return Physics2D.OverlapCircle(checkPosition, groundCheckRadius, groundMask) != null;
    }

    private Vector2 GetStandingPlatformVelocity()
    {
        Vector2 checkPosition = groundCheck != null
            ? groundCheck.position
            : transform.position + Vector3.down * 0.55f;

        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = groundMask,
            useTriggers = false
        };

        int hitCount = Physics2D.OverlapCircle(checkPosition, groundCheckRadius, filter, groundHits);
        for (int i = 0; i < hitCount; i++)
        {
            if (groundHits[i] == null)
            {
                continue;
            }

            MovingPlatform2D platform = groundHits[i].GetComponentInParent<MovingPlatform2D>();
            if (platform != null)
            {
                return platform.Velocity;
            }
        }

        return Vector2.zero;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 checkPosition = groundCheck != null
            ? groundCheck.position
            : transform.position + Vector3.down * 0.55f;
        Gizmos.DrawWireSphere(checkPosition, groundCheckRadius);
    }
}
