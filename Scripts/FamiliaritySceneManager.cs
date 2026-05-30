using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor; // Required for SceneAsset drag & drop in the Unity Editor
#endif

/// <summary>
/// SceneManagerVR handles scene transitions between the Familiarity Scene and the Main Experiment Scene.
/// - Allows switching scenes using the Enter key.
/// - Supports assigning SceneAssets in the Unity Editor.
/// - Retains and assigns the participant ID in a TMP Input Field.
/// - Toggles specific objects in the Main Scene.
/// 
/// REQUIREMENT:
/// // Make sure the scenes are added in the Build settings(And ticked) for the Scene names or assets to be recognised by the unity
/// </summary>
public class SceneManagerVR : MonoBehaviour
{

    [Header("Scene Settings")]
#if UNITY_EDITOR
    public SceneAsset familiaritySceneAsset; // Scene asset for Familiarity Scene (Editor only)
    public SceneAsset mainSceneAsset; // Scene asset for Main Experiment Scene (Editor only)
#endif
    public string familiarityScene; // Name of the Familiarity Scene (Used in Build)
    public string mainScene; // Name of the Main Experiment Scene (Used in Build)

    [Header("Player ID Settings")]
    public string participantID = "IDscript"; // Default participant ID
    public TMP_InputField idInputField; // TMP Input Field to display the participant ID

    [Header("Main Scene Objects to Toggle")]
    public GameObject[] objectsToToggle; // List of GameObjects to toggle when switching to the main scene

    private bool isFamiliaritySceneLoaded = false; // Tracks whether the Familiarity Scene is currently loaded

    void Awake()
    {
#if UNITY_EDITOR
        // Convert SceneAssets to scene names only in Unity Editor
        if (string.IsNullOrEmpty(familiarityScene) && familiaritySceneAsset != null)
        {
            familiarityScene = familiaritySceneAsset.name;
        }
        if (string.IsNullOrEmpty(mainScene) && mainSceneAsset != null)
        {
            mainScene = mainSceneAsset.name;
        }


#endif

    }

    void Start()
    {
        // Check if the currently active scene is the Familiarity Scene
        if (SceneManager.GetActiveScene().name == familiarityScene)
        {
            isFamiliaritySceneLoaded = true;
        }

        // If in the Main Scene, restore the participant ID
        if (SceneManager.GetActiveScene().name == mainScene)
        {
            AssignParticipantID();
        }
    }

    void Update()
    {
        // Detect Enter key press to switch scenes
        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            ToggleScenes();
        }

        // Continuously update the participant ID field if assigned
        if (idInputField != null)
        {
            idInputField.text = participantID;
        }

    }

    /// <summary>
    /// Toggles between the Familiarity Scene and the Main Experiment Scene.
    /// </summary>
    public void ToggleScenes()
    {
        // Determine the next scene based on the current scene
        string currentScene = SceneManager.GetActiveScene().name;
        string nextScene = (currentScene == familiarityScene) ? mainScene : familiarityScene;

        // Load the new scene and trigger OnSceneLoaded after loading
        SceneManager.LoadScene(nextScene);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// Handles post-load actions, such as setting the participant ID and toggling objects.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == mainScene)
        {
            FindObjectOfType<ExperimentManager>().ToggleStart();
            //FindObjectOfType<ExperimentManager>().StartExpButton.onClick.Invoke();


            AssignParticipantID(); // Assign the participant ID in the Main Scene

            // Toggle specified GameObjects
            foreach (GameObject obj in objectsToToggle)
            {
                if (obj != null)
                    obj.SetActive(!obj.activeSelf);
            }
        }

        // Unsubscribe from the event to prevent duplicate calls
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Assigns the participant ID to the TMP Input Field.
    /// </summary>
    private void AssignParticipantID()
    {
        if (idInputField == null)
        {
            idInputField = FindObjectOfType<TMP_InputField>(); // Find the input field dynamically
        }
        idInputField.text = participantID;
    }
}
