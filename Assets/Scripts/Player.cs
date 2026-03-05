using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    private Rigidbody rb;

    [SerializeField] float maxHorizontalSpeed;
    [SerializeField] float maxVerticleSpeed;
    [SerializeField] float acceleration;
    [SerializeField] float jumpingPower;
    [SerializeField] float turnSpeed;

    [SerializeField] Vector3 launchVelocity;
    [SerializeField] float slack;
    [SerializeField] float pullPower;

    [SerializeField] GrapplingHook hookPrefab;
    private GrapplingHook hookInstance = null;
    [SerializeField] new Camera camera;
    [SerializeField] LayerMask ground;
    [SerializeField] Transform groundChecker;

    private Vector2 horizontalControl;
    private Vector2 turnControl;
    private float timeSinceLastTurn;

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
        
        // add acceleration
        Vector3 additionalVector = (transform.rotation * new Vector3(horizontalControl.x, 0, horizontalControl.y)).normalized;
        Vector3 newXZVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z) + acceleration * Time.deltaTime * additionalVector;
        // cap speed
        if (newXZVelocity.magnitude > maxHorizontalSpeed) // IsOnGround() && 
        {
            newXZVelocity = newXZVelocity.normalized * maxHorizontalSpeed;
        }
        // apply hook pull
        if (hookInstance && hookInstance.IsHooked())
        {
            Vector3 difference = hookInstance.transform.position - transform.position;
            newXZVelocity += Mathf.Max(difference.magnitude-slack, 0) * pullPower * Time.deltaTime * (difference.normalized);
        }
        // update velocity
        rb.linearVelocity = new Vector3(newXZVelocity.x, rb.linearVelocity.y, newXZVelocity.z);
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
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpingPower, rb.linearVelocity.y);
        }
    }

    public void Turn(InputAction.CallbackContext context)
    {
        turnControl = context.ReadValue<Vector2>();
    }

    public void Hook(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed) {
            if (hookInstance) {
                if (hookInstance.IsHooked()) {
                    hookInstance = null;
                }
            } else {
                hookInstance = Instantiate(hookPrefab, transform.position, transform.rotation);
                hookInstance.SetVelocity(camera.transform.rotation*launchVelocity + rb.linearVelocity);
            }
        }
    }

    private bool IsOnGround()
    {
        return Physics.OverlapBox(groundChecker.position, new Vector3(0.5f, 0.1f, 0.5f), transform.rotation, ground).Length > 0;
    }

}
