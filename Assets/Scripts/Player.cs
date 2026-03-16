using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class Player : NetworkBehaviour
{
    private Rigidbody rb;

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
    [SerializeField] InputActionReference fireAction;
    [SerializeField] float minFireSpeed;
    [SerializeField] float maxFireSpeed;
    public float arrowPower;
    float bowTime;

    private Vector2 horizontalControl;
    private Vector2 turnControl;
    
    private LineRenderer lineRenderer;
    private PlayerInput playerInput;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        rb = GetComponent<Rigidbody>();
        lineRenderer = GetComponent<LineRenderer>();
        Cursor.lockState = CursorLockMode.Locked;
        bowTime = 0;
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            playerInput = GetComponent<PlayerInput>();
            playerInput.enabled = true;
        } else {
            camera.enabled = false;
            camera.GetComponent<AudioListener>().enabled = false;
        }
    }

    void Update()
    {
        if (!IsOwner) {
            return;
        }
        lineRenderer.SetPosition(0, transform.position + new Vector3(-0.5f, 0, 0));
        if (hookInstance) {
            lineRenderer.SetPosition(1, hookInstance.transform.position);
        } else {
            lineRenderer.SetPosition(1, transform.position + new Vector3(-0.5f, 0, 0));
        }
    }

    void FixedUpdate()
    {
        if (!IsOwner) {
            return;
        }
        UpdateBow();
        // turn
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + turnControl.x * turnSpeed, 0f);
        camera.transform.localRotation = Quaternion.Euler(camera.transform.localEulerAngles.x - turnControl.y * turnSpeed, 0f, 0f);
        turnControl = new Vector2(0, 0);

        // add acceleration
        Vector3 additionalVector = (transform.rotation * new Vector3(horizontalControl.x, 0, horizontalControl.y)).normalized;
        Vector3 oldXZVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
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
            if (difference.magnitude > maxHookDist)
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

        if (fireAction.action.IsPressed())
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
            if (arrow.activeSelf) LaunchArrow(t);
            arrow.SetActive(false);
        }
    }

    public void Move(InputAction.CallbackContext context)
    {
        if (!IsOwner) {
            return;
        }
        horizontalControl = context.ReadValue<Vector2>();
    }

    public void Jump(InputAction.CallbackContext context)
    {
        if (!IsOwner) {
            return;
        }
        // Check if the jump button was pressed and the player is grounded
        if (context.performed && IsOnGround())
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpingPower, rb.linearVelocity.z);
        }
    }

    public void Turn(InputAction.CallbackContext context)
    {
        if (!IsOwner) {
            return;
        }
        turnControl += context.ReadValue<Vector2>();
    }

    public void Hook(InputAction.CallbackContext context)
    {
        if (!IsOwner) {
            return;
        }
        if (context.phase == InputActionPhase.Performed)
        {

            if (hookInstance)
            {
                if (hookInstance.IsHooked())
                {
                    yanked = yankPower * (hookInstance.transform.position - transform.position);
                }
                Unhook();
            }
            else
            {
                hookInstance = Instantiate(hookPrefab, transform.position, transform.rotation);
                hookInstance.SetVelocity(GetLaunchVelocity(launchVelocity));
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
