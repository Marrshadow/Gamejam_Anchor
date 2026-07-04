using UnityEngine;
using UnityEngine.InputSystem;
using SKCell;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour, PlayerInputControl.IGamePlayActions
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float jumpForce = 13f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.16f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Actions")]
    [SerializeField] private AnchorLauncher2D anchorLauncher;

    private PlayerInputControl inputControl;
    private Rigidbody2D body;
    private Vector2 moveInput;
    private Vector2 pointerScreenPosition;
    private bool jumpQueued;

    private void Awake()
    {
        inputControl = new PlayerInputControl();
        body = GetComponent<Rigidbody2D>();
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (anchorLauncher == null)
        {
            anchorLauncher = GetComponent<AnchorLauncher2D>();
        }
    }

    private void OnEnable()
    {
        inputControl.GamePlay.AddCallbacks(this);
        inputControl.GamePlay.Enable();
    }

    private void OnDisable()
    {
        inputControl.GamePlay.RemoveCallbacks(this);
        inputControl.GamePlay.Disable();
    }

    private void OnDestroy()
    {
        inputControl.Dispose();
    }

    private void FixedUpdate()
    {
        body.linearVelocity = new Vector2(moveInput.x * moveSpeed, body.linearVelocity.y);

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
        if (!context.started)
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

        if (anchorLauncher != null && anchorLauncher.IsMouseBound)
        {
            anchorLauncher.BindAnchorToMouse(pointerScreenPosition);
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
        if (!context.started || anchorLauncher == null)
        {
            return;
        }

        anchorLauncher.BindAnchorToMouse(pointerScreenPosition);
    }

    public void OnCancle(InputAction.CallbackContext context)
    {
        if (!context.started || anchorLauncher == null)
        {
            return;
        }

        anchorLauncher.RetractAnchor();
    }

    private bool IsGrounded()
    {
        Vector2 checkPosition = groundCheck != null
            ? groundCheck.position
            : transform.position + Vector3.down * 0.55f;

        return Physics2D.OverlapCircle(checkPosition, groundCheckRadius, groundMask) != null;
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
