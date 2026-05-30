/*
 * ExperimentManager Script
 * -------------------------
 * Summary:
 * This script manages the flow of an experiment in Unity, including data capture, collision handling, 
 * UI updates, and audio playback. It ensures structured interaction with the environment through tagged 
 * and layered objects, and captures participant data for post-experiment analysis.

 * Functionalities:
 * 1. **Experiment Lifecycle Management:**
 *    - Start/stop the experiment via a UI button or collision with designated areas.
 *    - Toggle experiment states and update related UI elements dynamically with specified time delays

 * 2. **Collision Detection:**
 *    - Detect collisions with specific areas (e.g., start and finish zones) using capsule colliders.
 *    - Log collided objects and track participant progress.

 * 3. **Audio Playback:**
 *    - Play audio clips with optional delays and looping during the experiment.
 *    - Automatically stop and clean up audio sources when the experiment ends.

 * 4. **Object Management:**
 *    - Toggle the active state of GameObjects by tag or layer.
 *    - Enable or disable MeshRenderers for objects on specific layers.

 * 5. **Data Capture:**
 *    - Periodically log the camera's position, rotation, and collisions to a CSV file.
 *    - Save experiment summary metrics (e.g., elapsed time, distance travelled) to a text file.

 * 6. File i/o directory setup
 *    - Directory is initialised with a Startup prefix before start is pressed
 *    - The StartupID directory is only used for AnchorReinitiateManager to save the registration data before Start is pressed
 *    - Everytime Start is toggled On (ToggleStart), a new Directory (Folder with current timestap) is created.
 *    - And the previous registration results and Anchor data is resaved in this directory to keep everything in one place using saveFileatStart from AnchorReinitiateManager
 *    - ButtonActivation script saves the Log data using saveFileatStop() at when the experiment finishes(StopExperiment()) or interupted using Start toggled Off
 * 
 * 7. **UI Updates:**
 *    - Dynamically update UI instructions during different experiment phases.
 *    - Temporarily toggle UI elements on or off with timed delay when experiment starts and turns it on when experiment finishes

 * Key Methods:
 * - `StartExperiment()` / `StopExperiment(string)`: Handles the core lifecycle of the experiment.
 * - `ToggleStart()`: Toggles the experiment's initial state via the UI button.
 * - `ToggleGameObjectsWithTag(string, bool)` / `ToggleGameObjectsWithLayer(string, bool)`: Manages object states by tag or layer.
 * - `SaveData()`: Captures and logs participant data to a CSV file.
 * - `PlayClipWithDelay(AudioClip, float, bool)`: Plays audio clips with optional delay and looping.

 * Usage:
 * - Attach this script to a GameObject in the scene.
 * - Assign necessary references (UI elements, colliders, layers, etc.) in the Inspector.
 * - Ensure appropriate tags and layers are set up in the Unity Editor.
 * - Start the experiment using the UI button or programmatically via `ToggleStart()`.

 * Notes:
 * - Ensure proper setup of the file paths and necessary folders for data storage.
 * - Debug logs are included for troubleshooting and tracking script execution.
 * - Collision detection relies on proper tagging and collider configurations.
 */

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.InputSystem;

public class ExperimentManager : MonoBehaviour
{
    //In order for this script to be accessed by other scripts
    //public static ExperimentManager Instance;

    [Header("Experiment Settings")]
    public Camera cameraToSave; // The camera to track and save
    public GameObject colliderCapsule; // Capsule collider attached to the camera
    public GameObject startingArea; // The starting area trigger
    public GameObject finishArea1; // First finish area trigger
    public GameObject finishArea2; // Second finish area trigger
    public TextMeshProUGUI uiInstructions; // UI Text for showing experiment completion
    public TextMeshProUGUI DebugText; // Reference to the TMP Debug text



    [SerializeField] private TMP_InputField ExpID; // Input field for the file name

    //public GameObject OtherCapsule; // For testing only

    [SerializeField]
    public Button StartExpButton; // UI button to Start the experiment
    public static bool isStart = false; // Boolean value to toggle

    private Color defaultColor; // Default button color
    [Header("Layer Settings")]
    //[SerializeField]
    //private List<string> CollisionLayers = new List<string>{ "InvisibleLayer", "InvisibleLayer2" }; // List of layer names to include in collision detection
    public string invisibleLayer1 = "InvisibleLayer"; // Layer name of objects to turn invisible
    public string invisibleLayer2 = "InvisibleLayer2"; // Layer name of objects to turn invisible
    public string interactiveLayer = "InteractiveObjects"; // Layer name of objects to turn invisible
    public List<GameObject> objectsToInactivate; // Objects to deactivate

    [Header("Audio Clips")]
    public AudioClip clip1;
    public AudioClip clip2;
    private List<AudioSource> activeAudioSources = new List<AudioSource>(); // Keep track of active AudioSources


    [Header("Data Capture Settings")]
    public float captureInterval = 0.1f; // Frequency of data capture (in seconds)

    private string fileName; // CSV file name
    public static string filePath; // Path for CSV file
                                   //public static string FilePathRef => filePath; // for saving Objects Json, Registration and scaling info in the experiment folder


    //private List<string> collisionObjectNames = new(); // List of collided object names
    // Dictionary to track unique object collisions
    private Dictionary<int, string> collisionObjectNames = new Dictionary<int, string>();

    private Vector3 lastPosition; // Last recorded position
    private float totalDistanceTravelled = 0f; // Distance travelled
    private float elapsedTime = 0f; // Time elapsed in seconds
    private bool experimentStarted = false; // Flag to track experiment state
    private float captureTimer = 0f; // Timer for tracking capture intervals

    private StreamWriter csvWriter; // CSV file writer
    private DateTime startTime; // Time when experiment started

    private Collider[] hitColliders; // Store Collision results globally for reuse
    private int collisionCounter = 0; // Collision counter

    private List<ParticleSystem> particleSystems = new List<ParticleSystem>();


    //Test field - remove after debugging
    //private string InvisibleDebugText = "Not assigned";


        private void Start()
    {
        InitializeParticleSystems();

        // CHANGED: Immediately stop and clear all particle systems and disable Play On Awake to prevent any visuals at startup.
        foreach (ParticleSystem ps in particleSystems)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.playOnAwake = false;
        }

        // CHANGED: Turn off all particle effects at the beginning
        //TurnOffParticleEffects();

        AudioListener.volume = 0.0025f;
        Debug.Log("Scene audio reduced to low levels.");

        // Initialize the list of particle systems
        //InitializeParticleSystems();

        // Attach the ToggleFunction to the button's onClick event
        {
            if (StartExpButton != null)
            {
                // Save the default color of the button
                defaultColor = StartExpButton.image.color;

                // Add listener to the button's onClick event
                StartExpButton.onClick.AddListener(ToggleStart);

            }
            else
            {
                Debug.LogError("Button reference is missing!");
            }

            if (ExpID != null)
            {
                ExpID.text = "ID";
                ExpID.onValueChanged.AddListener(UpdateFilePath);
            }

            fileName = $"Startup_Participant_Data_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
            filePath = $"{Application.persistentDataPath}/FireEvacCivil/{DateTime.Now:yyyy-MM-dd}/{ButtonActivation.XRMode}_Startup_Exp{ExpID.text}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}/";
        }

        //Button click by force for testing
        //ToggleStart();

        //StartExpButton.onClick.Invoke();

        //StartCoroutine(ToggleGameObjectAfterSeconds(uiInstructions.gameObject, false, 5f));
        //StartCoroutine(ToggleTagAfterSeconds("UI1", false, 0.3f));

        //InvisibleDebugText = "chosen layers = " + invisibleLayer1;
        //Debug.Log($"InvisibleLayer is set to: {invisibleLayer1}");
        //ToggleMeshRenderersByLayer(invisibleLayer1, false);
        //ToggleMeshRenderersByLayer(invisibleLayer1, true);

        Debug.Log("Experiment Manager Initialized.");
    }
    private void UpdateFilePath(string newID)
    {
        filePath = $"{Application.persistentDataPath}/FireEvacCivil/{DateTime.Now:yyyy-MM-dd}/{ButtonActivation.XRMode}_Exp{newID}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}/";
        Debug.Log($"Experiment folder Path changed to {filePath}");
    }

    private void Update()
    {
        //Debug.Log($"isStart: {isStart}, experimentStarted: {experimentStarted}, CollidingWithStartingArea: {IsCollidingWith(startingArea)}, IsCollidingWith(finishArea1): {IsCollidingWith(finishArea1)} IsCollidingWith(finishArea2): {IsCollidingWith(finishArea2)}");
        //Debug.Log(colliderCapsule.gameObject.name + " Pos: " + colliderCapsule.transform.position); // + "; " + OtherCapsule.gameObject.name + "Pos: + " + OtherCapsule.transform.position);
        //Debug.Log("PhysicsIsCollidingWith: " + IsCollidingWith(OtherCapsule));
        //int mask = LayerMask.GetMask(CollisionLayers.ToArray());
        //Debug.Log($"Generated LayerMask: {mask}");
        //Debug.Log($"Mask Binary Representation: {Convert.ToString(mask, 2).PadLeft(32, '0')}");

        //Debug.Log($"Bools to StartExp: isStart={isStart} && !experimentStarted={!experimentStarted} && CollstartArea={IsCollidingWith(startingArea)}");
        UpdateUIText(DebugText); //For test debugging only

        // Get all colliders that intersect with the colliderCapsule for all the layers included in CollisionLayers string list
        hitColliders = Physics.OverlapCapsule(
            colliderCapsule.transform.position,
            colliderCapsule.transform.position,
            colliderCapsule.GetComponent<CapsuleCollider>().radius, // Adjust radius as needed
            LayerMask.GetMask(invisibleLayer1, invisibleLayer2, interactiveLayer) // Layer mask consisting all the layers needed for physics collision detection to work with
        );

        // Check for starting area trigger
        if (isStart && !experimentStarted && IsCollidingWith(startingArea))
        {
            StartExperiment();
        }


        // Check if experiment is running
        if (experimentStarted)
        {
            elapsedTime += Time.deltaTime;
            captureTimer += Time.deltaTime;

            // Save data at the specified interval
            if (captureTimer >= captureInterval)
            {
                SaveData();
                captureTimer = 0f;
            }

            // Check for finish areas
            if (IsCollidingWith(finishArea1) || IsCollidingWith(finishArea2))
            {
                StopExperiment(IsCollidingWith(finishArea1) ? "FinishArea1" : "FinishArea2");
            }
        }

        // If we’re running in the Unity Editor and space bar is pressed,
        // call the same method as if the button was clicked.
#if UNITY_EDITOR

        //Using the new Input system
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            // Option A: Directly call ToggleStart()
            // ToggleStart();

            // Option B: Invoke the button's click event 
            // (acts just like a real button press)
            StartExpButton.onClick.Invoke();
        }
#endif
    }

    // Function to toggle the isStart value
    //[Obsolete]
    public void ToggleStart()
    {
        // Toggle the bool value
        isStart = !isStart;
        Debug.Log($"ToggleStart called. isStart: {isStart}");

        // Change button color based on the Start state
        if (isStart)
        {
            // Initialize the list of particle systems
            InitializeParticleSystems();

            StartExpButton.image.color = Color.green; // Set to green

            uiInstructions.text = "Please approach the Green Area infront of you to start the experiment";
            ToggleMeshRenderersByLayer(invisibleLayer1, true);
            ToggleMeshRenderersByLayer(invisibleLayer2, false);
            ToggleMeshRenderersByLayer(interactiveLayer, false);
            ToggleMeshRenderersByLayer("RegObjects", false);


            TurnOffParticleEffects();
            MuteScene();

            //ToggleGameObjectsWithTag("RegistrationObjects", false);
            //ToggleGameObjectsWithTag("Start", true);

            Debug.Log("Start is ON");

            UpdateFilePath(ExpID.text); // Updating the Filepath directory for a fresh savepath for all the files for the current session
            FindObjectOfType<AnchorReinitiateManager>().saveFileatStart();

            // Deactivate objects from objectsToInactivate list
            ToggleObjList(objectsToInactivate, false);

        }
        else
        {
            uiInstructions.text = "Press Start to enter the Experitment mode";

            StartExpButton.image.color = defaultColor; // Revert to default color

            ToggleMeshRenderersByLayer(invisibleLayer1, true);
            ToggleMeshRenderersByLayer(invisibleLayer2, true);
            ToggleMeshRenderersByLayer(interactiveLayer, true);
            ToggleMeshRenderersByLayer("RegObjects", true);


            //TurnOnParticleEffects();
            UnmuteScene();
            FindObjectOfType<ButtonActivation>().saveFileatStop();


            //ToggleGameObjectsWithTag("RegistrationObjects", true);
            //ToggleGameObjectsWithTag("Start", true);

            if (experimentStarted)
            {
                StopExperiment("Experiment Interrupted using UI");
            }
            Debug.Log("Start is OFF");

            // Activate objects from objectsToInactivate list
            ToggleObjList(objectsToInactivate, true);
        }


    }

    public void ToggleObjList(List<GameObject> gameObjects, bool state)
    {
        foreach (var obj in gameObjects)
        {
            if (obj != null)
            {
                obj.SetActive(state);
                Debug.Log($"Set {obj.name} active: {state}");
            }
            else
            {
                Debug.LogWarning("Encountered a null object in the list.");
            }
        }
    }
    public void UpdateUIText(TextMeshProUGUI UIText)
    {
        if (UIText != null)
        {
            Vector3 currentPosition = cameraToSave.transform.position;
            UIText.text = $"expStarted: {experimentStarted} CamPos: {currentPosition.x},{currentPosition.y},{currentPosition.z} Colls: {IsCollidingWith(startingArea)},{IsCollidingWith(finishArea1)},{IsCollidingWith(finishArea2)} \n Bools to StartExp: {isStart} && {!experimentStarted} && {IsCollidingWith(startingArea)}";
            //Debug.Log($"Updated Design Debug UI Text"); // Optional: Log the updated text
        }
        else
        {
            Debug.LogWarning("TextMeshProUGUI reference is null. Cannot update text.");
        }
    }


    private void StartExperiment()
    {

        StartCoroutine(ToggleGameObjectAfterSeconds(uiInstructions.gameObject, false, 6f));
        //StartCoroutine(ToggleTagAfterSeconds("UI1", false, 2F)); //Or you can do it via Tag

        // Play each clip with the specified delay
        PlayClipWithDelay(clip1, 0f, true);
        //PlayClipWithDelay(clip1, 0f, false);
        //PlayClipWithDelay(clip2, clip1.length, false);
        //PlayClipWithDelay(clip1, clip1.length + clip2.length, true);


        experimentStarted = true;
        startTime = DateTime.Now;
        lastPosition = cameraToSave.transform.position;

        // Alarming sound
        uiInstructions.text = "There is a fire Emergency on this floor, please find a safe Exit Corridor";

        // Turn objects invisible - Deactivate MeshRenderer for objects on the layer "InvisibleLayer"
        //InvisibleDebugText = "chosen layers = " + invisibleLayer1;
        //Debug.Log($"InvisibleLayer is set to: {invisibleLayer1}");
        ToggleMeshRenderersByLayer(invisibleLayer1, false);
        ToggleMeshRenderersByLayer(interactiveLayer, true);
        ToggleMeshRenderersByLayer("RegObjects", false);

        TurnOnParticleEffects();
        UnmuteScene();

        //ToggleGameObjectsWithLayer(interactiveLayer, true);
        //ToggleGameObjectsWithTag("FireHazard", true);
        //ToggleGameObjectsWithTag("Start", false);




        if (experimentStarted == true)
        {
            fileName = $"{ExpID.text}_{ButtonActivation.XRMode}_Participant_Data_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";

            // Ensures the filepath directory exists
            Directory.CreateDirectory(filePath);
            Debug.Log($"New Directory created to save data at: {filePath} with filename: {fileName}");

            // Write the CSV header
            csvWriter = new StreamWriter(Path.Combine(filePath, fileName + ".csv"));
            csvWriter.WriteLine("Timestamp,ElapsedTime,PositionX,PositionY,PositionZ,RotationX,RotationY,RotationZ,RotationW,Collisions");
        }


        Debug.Log("Experiment started.");
    }


    private void StopExperiment(string exitChoice)
    {
        StopAllAudioSources();
        TurnOffParticleEffects();

        experimentStarted = false;

        // Play and display - Experiement has been finished audio
        StartCoroutine(ToggleGameObjectAfterSeconds(uiInstructions.gameObject, true, 0.5f));
        PlayClipWithDelay(clip2, 1f, false);

        // Update UI
        uiInstructions.text = "Experiment Finished! Please take off your Headset.";

        // Close the CSV writer
        csvWriter.Close();

        // Calculate metrics
        float averageSpeed = totalDistanceTravelled / elapsedTime;

        //fileName = $"Exp_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
        string summaryFilePath = Path.Combine(filePath, fileName + "_Summary.txt");

        // Write summary
        using (var summaryWriter = new StreamWriter(summaryFilePath))
        {
            summaryWriter.WriteLine($"Experiment Summary");
            summaryWriter.WriteLine($"-------------------");
            summaryWriter.WriteLine($"Start Time: {startTime:yyyy-MM-dd HH:mm:ss}");
            summaryWriter.WriteLine($"End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            summaryWriter.WriteLine($"Time Taken: {elapsedTime:F2} seconds");
            summaryWriter.WriteLine($"Exit Choice: {exitChoice}");
            summaryWriter.WriteLine($"Distance Travelled: {totalDistanceTravelled:F2} meters");
            summaryWriter.WriteLine($"Average Speed: {averageSpeed:F2} m/s");
            summaryWriter.WriteLine($"Number of Collisions: {collisionCounter}");
            summaryWriter.WriteLine($"Objects Collided With: {string.Join(", ", collisionObjectNames)}");
        }

        // Reactivate all objects
        ToggleMeshRenderersByLayer(invisibleLayer1, true);
        ToggleMeshRenderersByLayer(invisibleLayer2, true);
        ToggleMeshRenderersByLayer(interactiveLayer, false);


        FindObjectOfType<ButtonActivation>().saveFileatStop();


        //ToggleGameObjectsWithLayer(interactiveLayer, false);
        //ToggleGameObjectsWithTag("RegistrationObjects", true);
        //ToggleGameObjectsWithTag("Start", true);

        //ToggleObjList(objectsToInactivate, true);


        Debug.Log($"Experiment finished. Summary written to: {summaryFilePath}");
    }

    private void ToggleMeshRenderersByLayer(string layerName, bool enable)
    {
        //InvisibleDebugText = "Entered the Function";
        // Get the layer index from its name
        int layerIndex = LayerMask.NameToLayer(layerName);

        if (layerIndex == -1)
        {
            //InvisibleDebugText = $"Layer '{layerName}' does not exist. Please create it in Unity.";
            Debug.LogError($"Layer '{layerName}' does not exist. Please create it in Unity.");
            return;
        }

        // Find all objects in the scene
        GameObject[] allObjects = FindObjectsOfType<GameObject>();

        foreach (var obj in allObjects)
        {
            if (obj.layer == layerIndex)
            {
                // Get all MeshRenderers on the object (including children)
                MeshRenderer[] renderers = obj.GetComponentsInChildren<MeshRenderer>();

                foreach (var renderer in renderers)
                {
                    renderer.enabled = enable; // Enable or disable the renderer
                }

                Debug.Log($"{(enable ? "Enabled" : "Disabled")} MeshRenderer on {obj.name} (Layer: {layerName})");
                //InvisibleDebugText = $"{(enable ? "Enabled" : "Disabled")} MeshRenderer on {obj.name} (Layer: {layerName})";

            }
        }
    }


    public static void ToggleGameObjectsWithLayer(string layerName, bool state)
    {
        // PROBLEM: Can only turn the state off, it can not search for inactive objects in the scene with tag/layer
        /// <summary>
        /// Toggles the active state of all GameObjects in the scene with the specified layer.
        /// </summary>
        /// <param name="layerName">The name of the layer to search for.</param>
        /// <param name="state">The desired active state (true for active, false for inactive).</param>

        // Get the layer ID from the layer name
        int layer = LayerMask.NameToLayer(layerName);
        if (layer == -1)
        {
            Debug.LogError($"Layer '{layerName}' does not exist.");
            return;
        }

        // Find all GameObjects in the scene
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>(); // PROBLEM: Can only turn the state off, it can not search for inactive objects in the scene with tag/layer

        // Toggle active state for objects in the specified layer
        int toggledCount = 0;
        foreach (GameObject obj in allObjects)
        {
            if (obj.layer == layer)
            {
                obj.SetActive(state);
                toggledCount++;
            }
        }

        // Log the operation for debugging
        Debug.Log($"Set active state to '{state}' for {toggledCount} GameObjects in layer '{layerName}'.");
    }

    public static void ToggleGameObjectsWithTag(string tag, bool state)
    {
        // PROBLEM: Can only turn the state off, it can not search for inactive objects in the scene with tag/layer
        /// <summary>
        /// Toggles the active state of all GameObjects in the scene with the specified tag.
        /// </summary>
        /// <param name="tag">The tag to search for.</param>
        /// <param name="state">The desired active state (true for active, false for inactive).</param>

        // Find all GameObjects with the specified tag
        GameObject[] objectsWithTag = GameObject.FindGameObjectsWithTag(tag); // PROBLEM: Can only turn the state off, it can not search for inactive objects in the scene with tag/layer

        // Toggle the active state for each GameObject
        foreach (GameObject obj in objectsWithTag)
        {
            obj.SetActive(state);
        }

        // Log the operation for debugging
        Debug.Log($"Set active state to '{state}' for {objectsWithTag.Length} GameObjects with tag '{tag}'.");
    }


    private void SaveData()
    {
        Vector3 currentPosition = cameraToSave.transform.position;
        Quaternion currentRotation = cameraToSave.transform.rotation;

        // Calculate distance travelled since last frame
        totalDistanceTravelled += Vector3.Distance(lastPosition, currentPosition);
        lastPosition = currentPosition;

        // Track collisions
        //var collisions = GetCollisions();

        var collisionswithIDs = GetCollisionswithIDs();

        if (csvWriter == null)
        {
            Debug.LogError("CSV Writer is not initialized. Data will not be saved.");
            return;
        }

        // Write data to CSV
        csvWriter.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{elapsedTime * 1000},{currentPosition.x},{currentPosition.y},{currentPosition.z}," +
            $"{currentRotation.x},{currentRotation.y},{currentRotation.z},{currentRotation.w},{string.Join(" | ", collisionswithIDs)}");

        //// Update collision list
        //foreach (var obj in collisions)
        //{
        //    if (!collisionObjectNames.Contains(obj))
        //    {
        //        collisionObjectNames.Add(obj);
        //    }
        //}

        // Update collision list using Instance ID
        foreach (var obj in collisionswithIDs)
        {
            if (!collisionObjectNames.ContainsKey(obj.Key))
            {
                collisionObjectNames[obj.Key] = obj.Value; // Add unique collision
                collisionCounter += collisionswithIDs.Count;
                Debug.Log($"{collisionCounter}th Collison detected: {collisionObjectNames}");
            }
        }
    }


    private bool isColliding = false;
    private string targetObjectName; // Name of the target object to detect collision with

    private void OnCollisionEnter(Collision collision)
    {
        // Check if the collided object's name matches the target name
        if (collision.gameObject.name == targetObjectName)
        {
            isColliding = true;
            Debug.Log($"Collision started with: {collision.gameObject.name}");
        }
        Debug.Log($"Other Collision started with: {collision.gameObject.name}");
    }

    private void OnCollisionExit(Collision collision)
    {
        // Check if the exited object's name matches the target name
        if (collision.gameObject.name == targetObjectName)
        {
            isColliding = false;
            Debug.Log($"Collision ended with: {collision.gameObject.name}");
        }
        Debug.Log($"Other Collision ended with: {collision.gameObject.name}");

    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the name matches the target
        if (other.gameObject.name == targetObjectName)
        {
            isColliding = true;
        }
        Debug.Log($"Trigger Started with: {other.gameObject.name}");
    }

    private void OnTriggerExit(Collider other)
    {
        // Check if the name matches the target
        if (other.gameObject.name == targetObjectName)
        {
            isColliding = false;
            Debug.Log($"Trigger ended with: {other.gameObject.name}");
        }
    }

    private bool TriggerCollIsCollidingWith(GameObject area)
    {
        if (area == null || colliderCapsule == null)
        {
            Debug.Log($"Null Collidor: CollArea = {area} CaLCollier = {colliderCapsule}");
            return false;
        }

        // Update the target object name for comparison
        targetObjectName = area.name;

        return isColliding; // Return the collision status
    }

    private Dictionary<int, string> GetCollisionswithIDs()
    {
        Dictionary<int, string> collisions = new Dictionary<int, string>();

        foreach (var hit in hitColliders)
        {
            int instanceID = hit.gameObject.GetInstanceID();
            collisions[instanceID] = hit.gameObject.name;
        }

        //collisionCounter += collisions.Count;
        return collisions;
    }

    private List<string> GetCollisions()
    {
        List<string> collisions = new List<string>();
        //Collider[] hitColliders = Physics.OverlapCapsule(colliderCapsule.transform.position, colliderCapsule.transform.position, 0.5f);

        foreach (var hit in hitColliders)
        {
            collisions.Add(hit.gameObject.name);
        }

        //collisionCounter += collisions.Count;
        return collisions;
    }
    private bool IsCollidingWith(GameObject area)
    {
        // Check if the Target area/GameObject exists and has a valid tag
        if (area == null || colliderCapsule == null)
        {
            //Debug.Log($"Null or Untagged Collidor: CollArea = {area} CaLCollier = {colliderCapsule}");
            return false;
        }

        if (hitColliders == null || hitColliders.Length == 0)
        {
            //Debug.LogWarning("hitColliders is null or empty.");
            return false;
        }

        // Check if any collider matches the target name - Not working as Unity instantiates prefabs as clones
        // Use tags instead of names for comparison - Work way around
        foreach (var collider in hitColliders)
        {
            //Debug.Log($"Collision detected with: {collider.name}");
            //if (collider.gameObject.name == targetObjectName)
            if (collider.CompareTag(area.tag)) // Match by tag
            {
                //Debug.Log($"Key Collision detected with: {area.name}");
                return true;
            }
        }
        //Debug.Log($"No matching collisions found");
        return false; // No matching collisions found
    }

    private void PlayClipWithDelay(AudioClip clip, float delay, bool loop)
    {
        if (clip == null) return;

        AudioSource audioSource = gameObject.AddComponent<AudioSource>();
        // CHANGED: Ensure audio source does not play on awake.
        audioSource.playOnAwake = false;
        audioSource.clip = clip;
        audioSource.loop = loop;
        audioSource.PlayDelayed(delay);

        activeAudioSources.Add(audioSource);
        if (!loop)
        {
            Destroy(audioSource, clip.length + delay);
            StartCoroutine(RemoveFromListAfterTime(audioSource, clip.length + delay));
        }
    }

    // Stop all active AudioSources
    public void StopAllAudioSources()
    {
        foreach (var audioSource in activeAudioSources)
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                Destroy(audioSource); // Cleanup
            }
        }

        activeAudioSources.Clear(); // Clear the list
    }

    // Remove the AudioSource from the list after it is destroyed
    private System.Collections.IEnumerator RemoveFromListAfterTime(AudioSource audioSource, float delay)
    {
        yield return new WaitForSeconds(delay);
        activeAudioSources.Remove(audioSource);
    }

    private IEnumerator ToggleGameObjectAfterSeconds(GameObject obj, bool state, float delay)
    {
        // Wait for the given delay without freezing the game
        yield return new WaitForSeconds(delay);

        // Deactivate the GameObject
        obj.SetActive(state);

        Debug.Log($"GameObject '{obj.name}' is toggled '{state}' after {delay} seconds.");
    }


    private IEnumerator ToggleTagAfterSeconds(String tag, bool state, float delay)
    {
        // Wait for the given delay without freezing the game
        yield return new WaitForSeconds(delay);

        // Deactivate the GameObject with given tag
        //obj.SetActive(false);
        ToggleGameObjectsWithTag(tag, state);

        Debug.Log($"GameObjects with Tag: '{tag}' are toggled '{state}' after {delay} seconds.");
    }

    // Initialize the list of all particle systems in the scene
    public void InitializeParticleSystems()
    {
        particleSystems.Clear();

        // Get all root objects in the active scene
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (GameObject root in rootObjects)
        {
            // Include inactive children to find all particle systems
            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            particleSystems.AddRange(systems);
        }

        Debug.Log($"Found {particleSystems.Count} particle systems in the scene.");
    }

    // Turn on all particle effects
    public void TurnOnParticleEffects()
    {
        foreach (ParticleSystem ps in particleSystems)
        {
            if (!ps.isPlaying)
            {
                ps.Play();
            }
        }
        Debug.Log("Turned on all particle effects.");
    }

    // Turn off all particle effects
    public void TurnOffParticleEffects()
    {
        foreach (ParticleSystem ps in particleSystems)
        {
            if (ps.isPlaying)
            {
                // CHANGED: Stop emitting and clear existing particles immediately.
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
        Debug.Log("Turned off all particle effects.");
    }

    // Mute all audio in the scene
    public void MuteScene()
    {
        AudioListener.volume = 0f;
        Debug.Log("Scene audio muted.");
    }

    // Unmute all audio in the scene
    public void UnmuteScene()
    {
        AudioListener.volume = 0.5f;
        Debug.Log("Scene audio unmuted.");
    }

    private void OnDestroy()
    {
        if (csvWriter != null)
        {
            csvWriter.Close();
        }
    }
}
