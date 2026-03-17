using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    private Rigidbody rb;
    private Animator anim;

    [SerializeField] float stickTurnSpeed;
    [SerializeField] float maxHorizontalSpeed;
    [SerializeField] float maxAbsoluteSpeed;
    [SerializeField] float acceleration;
    [SerializeField] float jumpingPower;
    [SerializeField] float turnSpeed;

    [SerializeField] Vector3 launchVelocity;
    [SerializeField] float slack;
    [SerializeField] float pullPower;
    [SerializeField] float yankPower;
    [SerializeField] float maxHookDist;
    private Vector3 yanked;

    [SerializeField] GrapplingHook hookPrefab;
    private GrapplingHook hookInstance = null;

    [SerializeField] new Camera camera;
    [SerializeField] CollisionMonitor groundChecker;
    [SerializeField] float groundDrag;
    [SerializeField] float airDrag;

    [SerializeField] float maxBowTime;
    [SerializeField] GameObject arrow;
    [SerializeField] float bowDrawDistance;
    [SerializeField] float minFireSpeed;
    [SerializeField] float maxFireSpeed;
    bool bowHeld;
    public float arrowPower;
    float bowTime;

    private Vector2 horizontalControl;
    private Vector2 turnControl;
    private bool turnIsDelta = false;
    
    private LineRenderer lineRenderer;
    private PlayerInput playerInput;
    public PlayerManager manager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        lineRenderer = GetComponent<LineRenderer>();
        Cursor.lockState = CursorLockMode.Locked;
        bowTime = 0;
        bowHeld = false;
    }

    private void GameEnd()
    {
        manager.GameEnd(this);
    }

    void Update()
    {
        var lineOrigin = transform.position + new Vector3(-0.5f, 0, 0);
        lineRenderer.SetPosition(0, lineOrigin);
        if (hookInstance) {
            lineRenderer.SetPosition(1, hookInstance.transform.position);
        } else {
            lineRenderer.SetPosition(1, lineOrigin);
        }
    }

    void FixedUpdate()
    {
        if (manager.IsGameOver()) {
            return;
        }
        if (transform.position.y < -10) {
            GameEnd();
            return;
        }
        UpdateBow();

        // Speed, IsGrounded update
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        anim.SetFloat("Speed", horizontalVelocity.magnitude);
        anim.SetBool("IsGrounded", IsOnGround());

        // turn
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + turnControl.x * turnSpeed, 0f);
        camera.transform.localRotation = Quaternion.Euler(camera.transform.localEulerAngles.x - turnControl.y * turnSpeed, 0f, 0f);
        if (turnIsDelta) turnControl = Vector2.zero;

        // add acceleration
        Vector3 additionalVector = (transform.rotation * new Vector3(horizontalControl.x, 0, horizontalControl.y)).normalized;
        Vector3 oldXZVelocity = new (rb.linearVelocity.x, 0, rb.linearVelocity.z);
        Vector3 newVelocity = oldXZVelocity + acceleration * Time.deltaTime * additionalVector;
        // cap speed
        if (newVelocity.magnitude > maxHorizontalSpeed)
        {
            newVelocity = newVelocity.normalized * Mathf.Max(oldXZVelocity.magnitude, maxHorizontalSpeed);
        }
        newVelocity.y = rb.linearVelocity.y;
        // apply hook pull
        if (hookInstance)
        {
            Vector3 difference = hookInstance.transform.position - transform.position;
            if (difference.magnitude > maxHookDist || hookInstance.IsLoose())
            {
                Unhook();
            }
            else if (hookInstance.IsHooked())
            {
                // auto unhooks at distance
                if (difference.magnitude > slack)
                {
                    newVelocity += Mathf.Max(difference.magnitude - slack, 0) * pullPower * Time.deltaTime * (difference.normalized);
                }
            }
        }
        newVelocity += yanked;
        yanked = new Vector3(0, 0, 0);
        // apply drag
        newVelocity *= 1 - (IsOnGround() ? groundDrag : airDrag);
        if (newVelocity.magnitude > maxAbsoluteSpeed) {
            newVelocity = newVelocity.normalized * maxAbsoluteSpeed;
        }
        // update velocity
        rb.linearVelocity = newVelocity;
    }

    private void LaunchArrow(float strength)
    {
        var launched = Instantiate(arrow, arrow.transform.parent);
        launched.transform.parent = null;
        var rb = launched.AddComponent<Rigidbody>();
        rb.linearVelocity = GetLaunchVelocity(
            new(0, 0, Mathf.Lerp(minFireSpeed, maxFireSpeed, strength))
        );
        launched.GetComponent<BoxCollider>().enabled = true;
        launched.AddComponent<Arrow>().player = this;
    }

    // Controls
    private void UpdateBow()
    {
        float t = bowTime / maxBowTime;
        
        // IsDrawingBow update
        anim.SetBool("IsDrawingBow", bowHeld);

        if (bowHeld)
        {
            bowTime += Time.deltaTime;
        }
        else
        {
            bowTime = 0;
        }
        bowTime = Mathf.Min(bowTime, maxBowTime);

        if (bowTime > 0)
        {
            arrow.SetActive(true);
            float displacement = bowDrawDistance * (1 - Mathf.Pow(0.01f, t));
            arrow.transform.localPosition = new(0, 0, -displacement);
        }
        else
        {
            if (arrow.activeSelf){
                LaunchArrow(t);
                // trigger FireArrow
                anim.SetTrigger("FireArrow");
            }
            arrow.SetActive(false);
        }
    }

    public void Fire(InputAction.CallbackContext context)
    {
        bowHeld = context.ReadValue<float>() > 0.5;
    }

    public void Move(InputAction.CallbackContext context)
    {
        horizontalControl = context.ReadValue<Vector2>();
    }

    public void Jump(InputAction.CallbackContext context)
    {
        // Check if the jump button was pressed and the player is grounded
        if (context.performed && IsOnGround())
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpingPower, rb.linearVelocity.z);
        }
    }

    public void Turn(InputAction.CallbackContext context)
    {
        var turn = context.ReadValue<Vector2>();
        turnIsDelta = context.control.device.name == "Mouse"; // this is hacky, please fix
        if (turnIsDelta)
        {
            turnControl += turn;
        } else
        {
            turnControl = turn * stickTurnSpeed;
        }
    }

    public void Hook(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {

            if (hookInstance)
            {
                if (hookInstance.IsHooked())
                {
                    var toHook = hookInstance.transform.position - transform.position;
                    toHook *= toHook.magnitude;
                    yanked = yankPower * toHook;
                }
                Unhook();
            }
            else
            {
                hookInstance = Instantiate(hookPrefab, transform.position, transform.rotation);
                hookInstance.SetVelocity(GetLaunchVelocity(launchVelocity));

                // trigger ThrowHook
                anim.SetTrigger("ThrowHook");
            }
        }
    }

    private Vector3 GetLaunchVelocity(Vector3 launchVelocity)
    {
        return camera.transform.rotation * launchVelocity + rb.linearVelocity;
    }

    public void Unhook()
    {
        Destroy(hookInstance.gameObject);
        hookInstance = null;
    }

    private bool IsOnGround()
    {
        return groundChecker.IsColliding();
    }


}
