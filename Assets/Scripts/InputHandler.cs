using UnityEngine;
using UnityEngine.InputSystem;

public class InputHandler : MonoBehaviour
{
    [SerializeField] private InputActionAsset playerControls;

    private InputAction movementAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
    private InputAction interactAction;

    public Vector2 movementInput { get; private set; }
    public Vector2 lookInput { get; private set; }
    public bool jumpInput { get; private set; }
    public bool sprintInput { get; private set; }
    public bool crouchInput { get; private set; }
    public bool interactInput { get; private set; }

     private void OnEnable()
    {
        playerControls.FindActionMap("Player").Enable();
    }

    private void OnDisable()
    {
        playerControls.FindActionMap("Player").Disable();
    }

    private void Awake()
    {
        InputActionMap mapReference = playerControls.FindActionMap("Player");

        movementAction = mapReference.FindAction("Move");
        lookAction = mapReference.FindAction("Look");
        jumpAction = mapReference.FindAction("Jump");
        sprintAction = mapReference.FindAction("Sprint");
        crouchAction = mapReference.FindAction("Crouch");
        interactAction = mapReference.FindAction("Interact");

        MakeInputEvents();
    }

    private void MakeInputEvents()
    {
        movementAction.performed += ctx => movementInput = ctx.ReadValue<Vector2>();
        movementAction.canceled += ctx => movementInput = Vector2.zero;

        lookAction.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        lookAction.canceled += ctx => lookInput = Vector2.zero;

        jumpAction.performed += ctx => jumpInput = true;
        jumpAction.canceled += ctx => jumpInput = false;

        sprintAction.performed += ctx => sprintInput = true;
        sprintAction.canceled += ctx => sprintInput = false;

        crouchAction.performed += ctx => crouchInput = true;
        crouchAction.canceled += ctx => crouchInput = false;

        interactAction.performed += ctx => interactInput = true;
        interactAction.canceled += ctx => interactInput = false;
    }
}
