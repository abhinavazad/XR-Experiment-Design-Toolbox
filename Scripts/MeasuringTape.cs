/*
 * MeasuringTape.cs
 *
 * DESCRIPTION:
 * This script provides a distance measuring feature using a VR controller and a toggle button.
 * The user can create a start point (attached to the right controller), extrude a line to an end point,
 * and display the measured distance at the line’s midpoint, following these steps:
 *
 * WORKFLOW:
 * 1. Enable the measuring feature by turning on the "MeasureTape" toggle in the inspector.
 * 2. First press of the right-hand controller’s index trigger:
 *    - Instantiates a green sphere at the controller’s position and parents it to the controller
 *      (so it moves with the controller).
 * 3. Second press:
 *    - Detaches and fixes the green sphere in space.
 *    - Instantiates a thin cylindrical line at the same position and starts extruding it from
 *      the start sphere to the controller’s position in real-time.
 * 4. Third press:
 *    - Creates a second sphere at the controller’s current position, marking the end of the line.
 *    - Calculates and displays the distance in a red TextMeshPro object above the midpoint of the line.
 * 5. Use the "Clear Tape" button to destroy all measurement objects and reset the UI instructions.
 *
 * NOTE:
 * - The text offset is configurable in the editor (textOffset).
 * - UI instructions are updated at each step.
 * - If anything is unclear, please let me know!
 */

using UnityEngine;
using UnityEngine.UI;
using TMPro;
//using UnityEngine.XR.MagicLeap;

public class MeasuringTape : MonoBehaviour
{
    [Header("Toggle to turn on/off measuring tape")]
    [Tooltip("Set this to true when the measurement feature should be enabled.")]
    public bool measureTapeOn = true;

    [Header("Input Components")]
    public Transform Rcontroller_Transform;
    public TextMeshProUGUI uiInstructions;
    public Button clearTapeButton;

    [Header("Measurement Prefabs")]
    public GameObject spherePrefab;
    public GameObject cylinderPrefab;

    [Tooltip("Assign a prefab that already has a TextMeshPro component at the correct scale.")]
    public GameObject distanceTextPrefab;

    [Header("Text Offset")]
    [Tooltip("Vertical offset for the distance text above the line midpoint.")]
    public Vector3 textOffset = new Vector3(0, 0.1f, 0);

    // Internal references for measurement objects
    private GameObject startPointSphere;
    private GameObject measuringLine;
    private GameObject endPointSphere;
    private TextMeshPro distanceText;

    // Workflow states (0 = idle, 1 = start sphere attached, 2 = line extruding, 3 = measurement complete)
    private int measurementState = 0;

    private Vector3 fixedStartPosition;

#if MAGICLEAP
    // Magic Leap inputs to detect if the user is pressing Menu, Bumper, or Trigger.
    private MagicLeapInputs _magicLeapInputs;
    private MagicLeapInputs.ControllerActions _controllerActions;

    private void OnEnable()
    {
        // Initialize Input
        if (_magicLeapInputs == null)
        {
            _magicLeapInputs = new MagicLeapInputs();
            _controllerActions = new MagicLeapInputs.ControllerActions(_magicLeapInputs);
        }
        _magicLeapInputs.Enable();

        // Bind input actions
        //_controllerActions.Trigger.started += ctx => StartPlacingObject();
        //_controllerActions.Trigger.performed += ctx => FinalizePlacingObject();
        _controllerActions.Bumper.started += ctx => HandleInput();
    }

    private void OnDisable()
    {
        // Unsubscribe from controller input when the object is disabled
        if (_magicLeapInputs != null)
        {
            // UnBind input actions
            //_controllerActions.Trigger.started -= ctx => StartPlacingObject();
            //_controllerActions.Trigger.performed -= ctx => FinalizePlacingObject();
            _controllerActions.Bumper.started -= ctx => HandleInput();
            _magicLeapInputs.Disable();

        }
    }
#endif

    private void Start()
    {
        if (clearTapeButton != null)
        {
            clearTapeButton.onClick.AddListener(ClearMeasurements);
        }
        UpdateUI("MeasureTape is off. Turn on the toggle to start measuring.");
    }

    private void Update()
    {
        // Only process input if measuring feature is toggled ON
        if (!measureTapeOn)
        {
            return;
        }

#if OCULUS
        // Handle the right controller's index trigger press
        if (OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
        {
            HandleInput();
        }
#endif
        // If the user is in the extruding line state (measurementState == 2),
        // continuously update the line from the fixed start position to the controller.
        if (measurementState == 2 && measuringLine != null)
        {
            UpdateLine(measuringLine, fixedStartPosition, Rcontroller_Transform.position);
        }
    }

    private void HandleInput()
    {
        switch (measurementState)
        {
            case 0:
                // First press: Create and parent a green sphere to the controller
                startPointSphere = Instantiate(spherePrefab, Rcontroller_Transform.position, Quaternion.identity);
                startPointSphere.GetComponent<Renderer>().material.color = Color.green;
                startPointSphere.transform.SetParent(Rcontroller_Transform);

                measurementState = 1;
                UpdateUI("Green sphere attached. Press again to fix start point and extrude line.");
                break;

            case 1:
                // Second press: Fix the start sphere in place and start extruding a line
                // Detach from controller so it stays in the fixed position
                startPointSphere.transform.SetParent(null);
                fixedStartPosition = startPointSphere.transform.position;

                // Instantiate the cylinder line
                measuringLine = Instantiate(cylinderPrefab, fixedStartPosition, Quaternion.identity);
                measurementState = 2;
                UpdateUI("Line extruding. Press again to place endpoint and complete measurement.");
                break;

            case 2:
                // Third press: Place the end point sphere and finalize measurement
                endPointSphere = Instantiate(spherePrefab, Rcontroller_Transform.position, Quaternion.identity);

                // Finalize the line
                Vector3 endPosition = endPointSphere.transform.position;
                UpdateLine(measuringLine, fixedStartPosition, endPosition);

                // Compute distance
                float distance = Vector3.Distance(fixedStartPosition, endPosition);

                // Create a text object from the distanceTextPrefab
                if (distanceTextPrefab != null)
                {
                    // Instantiate at midpoint with the prefab’s scale preserved
                    Vector3 midPoint = (fixedStartPosition + endPosition) / 2f + textOffset;
                    GameObject textObject = Instantiate(distanceTextPrefab, midPoint, Quaternion.identity);
                    distanceText = textObject.GetComponent<TextMeshPro>();

                    // Update the text
                    distanceText.text = distance.ToString("F2") + " m";
                    distanceText.color = Color.red;
                }
                else
                {
                    Debug.LogWarning("No distanceTextPrefab assigned. Cannot display distance text.");
                }

                measurementState = 3;
                UpdateUI("Measurement complete. Press 'Clear Tape' to reset or turn off measureTape.");
                break;

            case 3:
                // If you need additional behavior after the third press, you can add it here
                UpdateUI("Measurement is already complete. Press 'Clear Tape' to reset.");
                break;
        }
    }

    private void UpdateLine(GameObject line, Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;
        float length = direction.magnitude;

        // Position the line at the midpoint
        line.transform.position = start + direction / 2;
        // Orient the line so its Z-axis aligns with the direction from start to end
        line.transform.rotation = Quaternion.LookRotation(direction);
        // Scale only the Z dimension of the cylinder to match the distance
        line.transform.localScale = new Vector3(0.01f, 0.01f, length);
    }

    private void ClearMeasurements()
    {
        if (startPointSphere != null) Destroy(startPointSphere);
        if (measuringLine != null) Destroy(measuringLine);
        if (endPointSphere != null) Destroy(endPointSphere);
        if (distanceText != null) Destroy(distanceText.gameObject);

        measurementState = 0;
        UpdateUI("Measurements cleared. Turn on the toggle and press the trigger to start measuring again.");
    }

    private void UpdateUI(string message)
    {
        if (uiInstructions != null)
        {
            uiInstructions.text = message;
        }
    }
}
