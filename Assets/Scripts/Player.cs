using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
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

    private Vector2 horizontalControl;
    private Vector2 turnControl;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        Cursor.lockState = CursorLockMode.Locked;
    }
    void FixedUpdate()
    {
        // turn
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + turnControl.x*turnSpeed, 0f);
        camera.transform.localRotation = Quaternion.Euler(camera.transform.localEulerAngles.x-turnControl.y*turnSpeed, 0f, 0f);
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
            if (difference.magnitude > maxHookDist) {
                Unhook();    
            }
            else if(hookInstance.IsHooked()) {
                // auto unhooks at distance
                if (difference.magnitude > slack) {
                    newVelocity += Mathf.Max(difference.magnitude-slack, 0) * pullPower * Time.deltaTime * (difference.normalized);
                }
            }
        }
        newVelocity += yanked;
        yanked = new Vector3(0,0,0);
        // apply drag
        newVelocity *= 1 - (IsOnGround() ? groundDrag : airDrag);
        // update velocity
        rb.linearVelocity = newVelocity;
    }
    
    // Controls

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
        turnControl += context.ReadValue<Vector2>();
    }

    public void Hook(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed) {
            
            if (hookInstance) {
                if (hookInstance.IsHooked()) {
                    yanked = yankPower * (hookInstance.transform.position - transform.position);
                }
                Unhook();
            } else {
                hookInstance = Instantiate(hookPrefab, transform.position, transform.rotation);
                hookInstance.SetVelocity(camera.transform.rotation * launchVelocity + rb.linearVelocity);
            }
        }
    }

    public void Unhook() {
        Destroy(hookInstance.gameObject);
        hookInstance = null;
    }

    private bool IsOnGround()
    {
        return groundChecker.IsColliding();
    }

}
