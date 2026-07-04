using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour, PlayerInputControl.IGamePlayActions, IAnchorFreezable
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float jumpForce = 13f;
    [SerializeField] private float coyoteTime = 0.08f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float moveInputDeadZone = 0.01f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.16f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Actions")]
    [SerializeField] private AnchorFreezeZone anchor;
    [SerializeField] private MouseRangeCircle2D mouseRangeCircle;
    [SerializeField] private bool anchorModeSwitchUnlocked;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string groundedParameter = "IsGround";

    [Header("Visual")]
    [SerializeField] private bool spriteFacesRight = false;
    [SerializeField] private bool startsFacingRight = true;

    private PlayerInputControl inputControl;
    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private Collider2D[] ownColliders;
    private Vector2 moveInput;
    private Vector2 pointerScreenPosition;
    private bool jumpQueued;
    private bool frozen;
    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpPressedTime = float.NegativeInfinity;
    private float animatorSpeed = 1f;
    private float cachedGravityScale = 1f;
    private readonly Collider2D[] groundHits = new Collider2D[8];
    private int speedParameterHash;
    private int groundedParameterHash;

    private void Awake()
    {
        inputControl = new PlayerInputControl();
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        ownColliders = GetComponentsInChildren<Collider2D>();
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        cachedGravityScale = body.gravityScale;
        CacheAnimator();
        CacheAnimatorSpeed();
        ApplyFacing(startsFacingRight ? 1f : -1f);

        if (anchor == null)
        {
            anchor = GetComponentInChildren<AnchorFreezeZone>();
        }

        if (mouseRangeCircle == null)
        {
            mouseRangeCircle = GetComponentInChildren<MouseRangeCircle2D>();
        }

        if (mouseRangeCircle == null)
        {
            mouseRangeCircle = CreateMouseRangeCircle();
        }

        if (mouseRangeCircle != null)
        {
            mouseRangeCircle.BindPlayer(transform);
        }

        if (anchor != null)
        {
            anchor.BindOwner(transform);
            anchor.BindMouseRangeCircle(mouseRangeCircle);
        }
    }

    private MouseRangeCircle2D CreateMouseRangeCircle()
    {
        GameObject circleObject = new GameObject("MouseRangeCircle");
        circleObject.transform.SetParent(transform, false);
        circleObject.transform.localPosition = Vector3.zero;
        return circleObject.AddComponent<MouseRangeCircle2D>();
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
            UpdateAnimation(0f, IsGrounded());
            return;
        }

        bool isGrounded = IsGrounded();
        if (isGrounded)
        {
            lastGroundedTime = Time.time;
        }

        Vector2 platformVelocity = Vector2.zero;
        bool isOnMovingPlatform = TryGetStandingPlatformVelocity(out platformVelocity);
        Vector2 nextVelocity = body.linearVelocity;
        nextVelocity.x = moveInput.x * moveSpeed + platformVelocity.x;
        bool shouldJump = HasBufferedJump() && CanUseGroundJump(isGrounded);

        if (!shouldJump && isOnMovingPlatform && platformVelocity.y > nextVelocity.y)
        {
            nextVelocity.y = platformVelocity.y;
        }

        body.linearVelocity = nextVelocity;

        if (!shouldJump)
        {
            UpdateAnimation(GetAnimatedMoveSpeed(platformVelocity), isGrounded);
            return;
        }

        body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
        body.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        jumpQueued = false;
        lastJumpPressedTime = float.NegativeInfinity;
        lastGroundedTime = float.NegativeInfinity;
        UpdateAnimation(GetAnimatedMoveSpeed(platformVelocity), false);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if (frozen)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = context.ReadValue<Vector2>();
        moveInput.x = Mathf.Abs(moveInput.x) > moveInputDeadZone ? Mathf.Clamp(moveInput.x, -1f, 1f) : 0f;

        if (Mathf.Abs(moveInput.x) > moveInputDeadZone)
        {
            ApplyFacing(moveInput.x);
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (frozen || !context.started)
        {
            return;
        }

        jumpQueued = true;
        lastJumpPressedTime = Time.time;
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
        if (!context.started || !anchorModeSwitchUnlocked || anchor == null)
        {
            return;
        }

        anchor.ToggleFreezeMode();
    }

    public void UnlockAnchorModeSwitch()
    {
        anchorModeSwitchUnlocked = true;
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

        if (animator != null)
        {
            if (freeze)
            {
                animatorSpeed = animator.speed;
            }

            animator.speed = freeze ? 0f : animatorSpeed;
        }

        UpdateAnimation(0f, IsGrounded());
    }

    private bool HasBufferedJump()
    {
        if (!jumpQueued)
        {
            return false;
        }

        if (Time.time - lastJumpPressedTime <= jumpBufferTime)
        {
            return true;
        }

        jumpQueued = false;
        return false;
    }

    private bool CanUseGroundJump(bool isGrounded)
    {
        return isGrounded || Time.time - lastGroundedTime <= coyoteTime;
    }

    private float GetAnimatedMoveSpeed(Vector2 platformVelocity)
    {
        float playerVelocityX = body.linearVelocity.x - platformVelocity.x;
        if (moveSpeed <= 0f)
        {
            return Mathf.Abs(moveInput.x);
        }

        return Mathf.Clamp01(Mathf.Abs(playerVelocityX) / moveSpeed);
    }

    private void ApplyFacing(float horizontalDirection)
    {
        if (Mathf.Abs(horizontalDirection) <= moveInputDeadZone)
        {
            return;
        }

        bool faceRight = horizontalDirection > 0f;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (faceRight == spriteFacesRight ? 1f : -1f);
        transform.localScale = scale;
    }

    private bool IsGrounded()
    {
        Vector2 checkPosition = GetGroundCheckPosition();
        int hitCount = GetGroundHits(checkPosition);
        for (int i = 0; i < hitCount; i++)
        {
            if (IsValidGroundHit(groundHits[i]))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetStandingPlatformVelocity(out Vector2 platformVelocity)
    {
        platformVelocity = Vector2.zero;
        Vector2 checkPosition = GetGroundCheckPosition();

        int hitCount = GetGroundHits(checkPosition);
        for (int i = 0; i < hitCount; i++)
        {
            if (!IsValidGroundHit(groundHits[i]))
            {
                continue;
            }

            MovingPlatform2D platform = groundHits[i].GetComponentInParent<MovingPlatform2D>();
            if (platform != null)
            {
                platformVelocity = platform.Velocity;
                return true;
            }
        }

        return false;
    }

    private int GetGroundHits(Vector2 checkPosition)
    {
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = groundMask,
            useTriggers = false
        };

        return Physics2D.OverlapCircle(checkPosition, groundCheckRadius, filter, groundHits);
    }

    private bool IsValidGroundHit(Collider2D candidate)
    {
        return candidate != null && !candidate.isTrigger && !IsOwnCollider(candidate);
    }

    private bool IsOwnCollider(Collider2D candidate)
    {
        if (candidate == null || ownColliders == null)
        {
            return false;
        }

        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (ownColliders[i] == candidate)
            {
                return true;
            }
        }

        return false;
    }

    private Vector2 GetGroundCheckPosition()
    {
        if (bodyCollider != null)
        {
            Bounds bounds = bodyCollider.bounds;
            return new Vector2(bounds.center.x, bounds.min.y - 0.02f);
        }

        return groundCheck != null
            ? groundCheck.position
            : transform.position + Vector3.down * 0.55f;
    }

    private void CacheAnimator()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        speedParameterHash = Animator.StringToHash(speedParameter);
        groundedParameterHash = Animator.StringToHash(groundedParameter);
    }

    private void CacheAnimatorSpeed()
    {
        if (animator != null)
        {
            animatorSpeed = animator.speed;
        }
    }

    private void UpdateAnimation(float horizontalSpeed, bool isGrounded)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetFloat(speedParameterHash, horizontalSpeed);
        animator.SetBool(groundedParameterHash, isGrounded);
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
