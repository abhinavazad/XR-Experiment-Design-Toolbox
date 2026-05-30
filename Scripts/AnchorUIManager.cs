using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SocialPlatforms.Impl;
using System.IO;

/*
 * Anchor Design Script
 * -------------------------
 * Summary:


 * Functionalities:



 * Usage:


 * Notes:
 * - Oculus platform uses _anchorInstances which is an inbuilt List<OVRSpatialAnchor> and magicleap uses a custom _anchorDataList, both oculus and magicleap can run using the later(_anchorDataList) but for now im keep it.
 * - Using List<OVRSpatialAnchor> -> it can assign inbuilt native Oculus's MR anchor feature, there is similar feature for ML to assign from ML too
 * - Latest: Based on customs _anchorDataList only, YAY!
 */


public class AnchorUIManager : MonoBehaviour
{
    //In order for this script to be accessed by other scripts
    public static AnchorUIManager Instance;

    [SerializeField]
    private TMP_Dropdown prefabDropdown;

    [SerializeField]
    private List<GameObject> placeablePrefabs; // Prefabs for anchors,
    // Public property for read-only access
    public List<GameObject> PlaceablePrefabs => placeablePrefabs;
    //Then any changes to placeablePrefabs will automatically reflect in PlaceablePrefabs, because PlaceablePrefabs is simply a read-only reference to the private field placeablePrefabs.
    //The property does not create a copy; it directly exposes the current state of placeablePrefabs.

    [SerializeField]
    private Transform _saveableTransform;

    private GameObject _currentPlacingObject; // Object being dynamically placed

    [SerializeField] private Button clearAnchorsButton; // UI button to clear all anchors
    [SerializeField] private Button deletelastAnchor; // UI button to clear all anchors
    [SerializeField] private Button UndolastdeletedAnchor; // UI button to clear all anchors

    private HashSet<Guid> _anchorUuids = new(); // Simulated external location, like PlayerPrefs


    [Serializable]
    public class AnchorData
    {
        public string PrefabName; // Name of the prefab
        public GameObject Prefab; // Reference to the original prefab
        public Vector3 Position; // Global position
        public Quaternion Rotation; // Global rotation
        public Guid Uuid; // Anchor UUID
    }
    [SerializeField] private TMP_InputField fileNameInputField; // Input field for the file name
    [SerializeField] private string fileName = "Anchor_data";

    private string filePath;// = $"/storage/emulated/0/Documents/FireEvacCivil/Anchors/";
    private string anchorDataFilePath; // = "/storage/emulated/0/Documents/FireEvacCivil/Anchors/Anchor_data.json"; //$"{Application.persistentDataPath}/anchors.json";
    public string AnchorDataFilePathRef => filePath;

    public TextMeshProUGUI DebugText; // Reference to the TMP Debug text
    public TextMeshProUGUI DisplayJsonText; // Reference to the TMP Debug text



    //private Stack<OVRSpatialAnchor> _deletedAnchors = new(); // Stack for undoing last deleted anchors
    private Stack<AnchorData> _deletedAnchors = new(); // Stack for storing deleted anchor data

    private List<AnchorData> _anchorDataList = new(); // Master list of anchor data
    private string jsonData; // Declare jsonData as a class-level variable

#if OCULUSX
    private Action<bool, OVRSpatialAnchor.UnboundAnchor> _onLocalized;
    private List<OVRSpatialAnchor> _anchorInstances = new(); // Active instances
#endif

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
        _controllerActions.Bumper.started += ctx => StartPlacingObject();
        //_controllerActions.Bumper.performed += ctx => UpdatePlacingObject();
        _controllerActions.Bumper.canceled += ctx => FinalizePlacingObject();
    }

    private void OnDisable()
    {
        // Unsubscribe from controller input when the object is disabled
        if (_magicLeapInputs != null)
        {
            // UnBind input actions
            _controllerActions.Bumper.started -= ctx => StartPlacingObject();
            //_controllerActions.Bumper.performed -= ctx => UpdatePlacingObject();
            _controllerActions.Bumper.canceled -= ctx => FinalizePlacingObject();
            _magicLeapInputs.Disable();
        }
    }
#endif


    public class AnchorUuidHandler : MonoBehaviour
    {
        public Guid Uuid;

        public void SetUuid(Guid uuid)
        {
            Uuid = uuid;
        }

        public Guid GetUuid()
        {
            return Uuid;
        }
    }


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
#if OCULUSX
            _onLocalized = OnLocalized;
#endif
        }
        else
        {
            Destroy(this);
        }

        // Link the Clear Anchors button click to ClearAllAnchorsFromScene
        if (clearAnchorsButton != null)
        {
            clearAnchorsButton.onClick.AddListener(ClearAllAnchorsFromScene);
        }
        if (deletelastAnchor != null)
        {
            deletelastAnchor.onClick.AddListener(DeleteLastAnchor);
        }
        if (UndolastdeletedAnchor != null)
        {
            UndolastdeletedAnchor.onClick.AddListener(UndoLastDeletedAnchor);
        }
    }

    private void Start()
    {
        Debug.Log($"Initial fileName: {fileName}, Input Field Text: {fileNameInputField.text}");

        filePath = $"{Application.persistentDataPath}/AnchorsData/";
        anchorDataFilePath = Path.Combine(filePath, fileName + ".json");



        try
        {
            string jsonContentstart = File.ReadAllText(anchorDataFilePath);
            //var parsedJson = JsonUtility.FromJson<object>(jsonContentstart); // Parse JSON
            //string prettyPrintedJson = JsonUtility.ToJson(parsedJson, true); // Pretty print with indentation

            Debug.Log($"Found existing Anchor data at {anchorDataFilePath}");

            UpdateUIText(DisplayJsonText, jsonContentstart);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error reading file: {ex.Message}");
        }

        PopulateDropdownOptions();


        if (fileNameInputField == null)
        {
            Debug.LogError("fileNameInputField is not assigned in the Inspector!");
        }
        fileNameInputField.text = fileName; //fileName

        fileNameInputField.onValueChanged.AddListener(UpdateFileName); // Listen for real-time changes
    }

    private void UpdateFileName(string newName)
    {
        fileName = newName; // Update the base name in real-time
        Debug.Log($"File name updated to: {fileName}");
    }

    private void PopulateDropdownOptions()
    {
        // Clear existing options
        prefabDropdown.ClearOptions();

        // Create a new list to hold the names of the prefabs
        List<string> prefabNames = new List<string>();

        // Loop through each prefab and add its name to the list
        foreach (var prefab in placeablePrefabs)
        {
            if (prefab != null)
            {
                prefabNames.Add(prefab.name); // Add the prefab's name
            }
            else
            {
                prefabNames.Add("Unnamed Prefab"); // Fallback for null or unnamed prefabs
            }
        }

        // Add the names to the dropdown as options
        prefabDropdown.AddOptions(prefabNames);

        Debug.Log("Dropdown options populated with prefab names.");
    }



    void Update()
    {
        anchorDataFilePath = Path.Combine(filePath, fileName + ".json");
#if OCULUS
        if (OVRInput.GetDown(OVRInput.Button.SecondaryHandTrigger)) // Trigger pressed
        {
            StartPlacingObject();
        }
        else if (OVRInput.Get(OVRInput.Button.SecondaryHandTrigger)) // Trigger held
        {
            UpdatePlacingObject();
        }
        else if (OVRInput.GetUp(OVRInput.Button.SecondaryHandTrigger)) // Trigger released
        {
            FinalizePlacingObject();
        }
        else if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger)) // Primary Hand Trigger to undo last deleted anchor
        {
            UndoLastDeletedAnchor();
        }
        else if (OVRInput.GetDown(OVRInput.Button.One)) // Button A to load saved anchors
        {
            LoadAllAnchors();
        }
        else if (OVRInput.GetDown(OVRInput.Button.Two)) // Button B to delete last added anchor
        {
            DeleteLastAnchor();
        }
#elif MAGICLEAP
        if (_currentPlacingObject != null)
        {
            UpdatePlacingObject();

            // Example: OVRInput.Button.SecondaryHandTrigger for "placing" an object
            //if (OVRInput.GetUp(OVRInput.Button.SecondaryHandTrigger))
            //{
            //    FinalizePlacingObject();
            //}
        }
#endif

    }


    private void StartPlacingObject()
    {
        //Debug.Log($"Entered the StartPlacingObject function");

        int selectedIndex = prefabDropdown.value;
        if (selectedIndex >= 0 && selectedIndex < placeablePrefabs.Count)
        {
            var selectedPrefab = placeablePrefabs[selectedIndex];

            // Instantiate the object at the controller's position and rotation
            _currentPlacingObject = Instantiate(selectedPrefab, _saveableTransform.position, _saveableTransform.rotation);

            Debug.Log("Started placing object.");
        }
        else
        {
            Debug.LogWarning("Invalid prefab selection.");
        }
    }


    private void UpdatePlacingObject()
    {
        //Debug.Log($"Entered the UpdatePlacingObject function");

        if (_currentPlacingObject != null)
        {
            // Update position to follow the controller (_saveableTransform)
            _currentPlacingObject.transform.position = _saveableTransform.position;

            //_currentPlacingObject.transform.rotation = _saveableTransform.rotation;

            // Freeze X and Z rotation while keeping Y rotation of the Controller (_saveableTransform)
            var rotation = _saveableTransform.rotation.eulerAngles;
            _currentPlacingObject.transform.rotation = Quaternion.Euler(0, rotation.y, 0);
        }
    }

    private void FinalizePlacingObject()
    {
        if (_currentPlacingObject != null)
        {

            // Freeze X and Z rotation before finalizing placement
            var rotation = _currentPlacingObject.transform.rotation.eulerAngles;
            _currentPlacingObject.transform.rotation = Quaternion.Euler(0, rotation.y, 0);

#if OCULUSX
            var anchor = _currentPlacingObject.AddComponent<OVRSpatialAnchor>();
            SetupAnchorAsync(anchor, saveAnchor: true);
#elif MAGICLEAP|| OCULUS
            var anchorData = new AnchorData
            {
                PrefabName = placeablePrefabs[prefabDropdown.value].name,
                Prefab = _currentPlacingObject,
                Position = _currentPlacingObject.transform.position,
                Rotation = _currentPlacingObject.transform.rotation,
                Uuid = Guid.NewGuid()
            };
            AddAnchorToList(anchorData);
            SaveAnchorDataToFile();
#endif

            _currentPlacingObject = null; // Clear the reference to the current object
            Debug.Log("Object placement finalized with X and Z rotations frozen.");
            UpdateUIText(DisplayJsonText, jsonData);
        }
    }


    public void UpdateUIText(TextMeshProUGUI UIText, string message)
    {
        if (UIText != null)
        {
            UIText.text = message; // Update the text
            //Debug.Log($"Updated UI Text: {message}"); // Optional: Log the updated text
        }
        else
        {
            Debug.LogWarning("TextMeshProUGUI reference is null. Cannot update text.");
        }
    }

#if OCULUSX
    private async void SetupAnchorAsync(OVRSpatialAnchor anchor, bool saveAnchor)
    {
        if (!await anchor.WhenLocalizedAsync())
        {
            Debug.LogError($"Unable to create anchor.");
            Destroy(anchor.gameObject);
            return;
        }

        _anchorInstances.Add(anchor);

        if (saveAnchor && (await anchor.SaveAnchorAsync()).Success)
        {
            _anchorUuids.Add(anchor.Uuid);

            // Add to the anchor data list
            var anchorData = StoreAnchorData(anchor);
            AddAnchorToList(anchorData);

            // Save the updated anchor data to file
            SaveAnchorDataToFile();
        }
    }

    private AnchorData StoreAnchorData(OVRSpatialAnchor anchor)
    {
        return new AnchorData
        {
            PrefabName = placeablePrefabs[prefabDropdown.value].name, //_currentPlacingObject.name, // Store the name of the prefab
            Prefab = _currentPlacingObject,
            Position = anchor.transform.position,
            Rotation = anchor.transform.rotation,
            Uuid = anchor.Uuid
        };
    }
#endif

    private void AddAnchorToList(AnchorData anchorData)
    {
        _anchorDataList.Add(anchorData);
        Debug.Log($"Anchor added to list: {anchorData.Uuid}");
    }

    private void RemoveAnchorFromList(Guid uuid)
    {
        _anchorDataList.RemoveAll(data => data.Uuid == uuid);
        Debug.Log($"Anchor removed from list: {uuid}");
    }


    private void SaveAnchorDataToFile()
    {
        try
        {
            // Ensures the filepath directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(anchorDataFilePath));

            jsonData = JsonUtility.ToJson(new AnchorDataListWrapper { Anchors = _anchorDataList }, prettyPrint: true);
            Debug.Log($"_anchorDataList count: {_anchorDataList.Count}");//, jsonData: {jsonData}");

            System.IO.File.WriteAllText(anchorDataFilePath, jsonData);
            //System.IO.File.WriteAllText(_anchorDataList + ".txt", jsonData);
            Debug.Log($"Anchor data saved to {anchorDataFilePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save anchor data: {ex.Message}");
        }
    }

    // Wrapper class to serialize lists in JSON (Unity's JsonUtility requires this)
    [Serializable]
    private class AnchorDataListWrapper
    {
        public List<AnchorData> Anchors;
    }




    /******************* Load Anchor Methods **********************/
    public async void LoadAllAnchors()
    {
#if OCULUSX
        if (_anchorUuids.Count == 0)
        {
            Debug.Log("No saved anchors to load.");
            return;
        }

        var unboundAnchors = new List<OVRSpatialAnchor.UnboundAnchor>();
        var result = await OVRSpatialAnchor.LoadUnboundAnchorsAsync(_anchorUuids, unboundAnchors);

        if (result.Success)
        {
            foreach (var unboundAnchor in unboundAnchors)
            {
                // Localize and bind unbound anchors to GameObjects
                unboundAnchor.LocalizeAsync().ContinueWith(_onLocalized, unboundAnchor);
            }
            Debug.Log($"Successfully loaded {unboundAnchors.Count} anchors.");
        }
        else
        {
            Debug.LogError($"Load anchors failed with {result.Status}.");
        }
#elif MAGICLEAP|| OCULUS
        // Load anchors stored in the JSON file
        anchorDataFilePath = Path.Combine(filePath, fileName + ".json");

        if (!File.Exists(anchorDataFilePath))
        {
            Debug.Log("No saved anchors to load.");
            return;
        }

        try
        {
            var jsonContent = File.ReadAllText(anchorDataFilePath);
            var loadedAnchors = JsonUtility.FromJson<AnchorDataListWrapper>(jsonContent);

            foreach (var anchorData in loadedAnchors.Anchors)
            {
                // Instantiate anchor GameObjects and place them at saved positions
                var go = Instantiate(anchorData.Prefab, anchorData.Position, anchorData.Rotation);
                AddAnchorToList(anchorData);
            }

            Debug.Log($"Successfully loaded {loadedAnchors.Anchors.Count} anchors from file.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load anchors: {ex.Message}");
        }
#endif
    }


#if OCULUSX
    private void OnLocalized(bool success, OVRSpatialAnchor.UnboundAnchor unboundAnchor)
    {
        var pose = unboundAnchor.Pose;
        int selectedIndex = prefabDropdown.value;
        var go = Instantiate(placeablePrefabs[selectedIndex], pose.position, pose.rotation);
        var anchor = go.AddComponent<OVRSpatialAnchor>();

        unboundAnchor.BindTo(anchor);
        _anchorInstances.Add(anchor);
    }
#endif

    /******************* Delete Last Anchor *****************/
    private void DeleteLastAnchor()
    {
        if (_anchorDataList.Count > 0)
        {
            var lastAnchor = _anchorDataList[^1];

            // Push the deleted anchor onto the stack for undo functionality
            _deletedAnchors.Push(new AnchorData
            {
                PrefabName = lastAnchor.PrefabName,
                Prefab = lastAnchor.Prefab, // Store the reference to the instantiated GameObject
                Position = lastAnchor.Position,
                Rotation = lastAnchor.Rotation,
                Uuid = lastAnchor.Uuid
            });

            // Remove from the anchor data list
            _anchorDataList.RemoveAt(_anchorDataList.Count - 1);

#if OCULUSX
            // Remove from active Oculus anchor instances
            var instance = _anchorInstances.Find(anchor => anchor.Uuid == lastAnchor.Uuid);
            if (instance != null)
            {
                Destroy(instance.gameObject);
                _anchorInstances.Remove(instance);
            }
            
            // Remove UUID from saved list
            if (_anchorUuids.Contains(lastAnchor.Uuid))
            {
                _anchorUuids.Remove(lastAnchor.Uuid);
            }

#endif

#if MAGICLEAP || OCULUS
            // Destroy the GameObject associated with the last anchor
            if (lastAnchor.Prefab != null)
        {
            Destroy(lastAnchor.Prefab);
        }
#endif


            // Save the updated anchor data to file
            SaveAnchorDataToFile();

            Debug.Log("Last anchor deleted.");
        }
        else
        {
            Debug.Log("No anchors to delete.");
        }
    }




    /******************* Undo Last Deleted Anchor *****************/
    private void UndoLastDeletedAnchor()
    {
        if (_deletedAnchors.Count > 0)
        {
            var lastDeleted = _deletedAnchors.Pop();

            // Check if the prefab reference is valid
            if (lastDeleted.Prefab == null)
            {
                Debug.LogError("lastDeleted.Prefab == null: So, instantiating by lastDeleted.PrefabName");
                lastDeleted.Prefab = placeablePrefabs.Find(p => p.name == lastDeleted.PrefabName);

            }

            // Instantiate a new GameObject for the restored anchor
            var restoredGameObject = Instantiate(
                lastDeleted.Prefab,
                lastDeleted.Position,
                lastDeleted.Rotation
            );

#if OCULUSX
            // Add an OVRSpatialAnchor component to the restored GameObject
            var anchor = restoredGameObject.AddComponent<OVRSpatialAnchor>();

            // Track this anchor with its UUID for internal management
            _anchorInstances.Add(anchor);
            _anchorUuids.Add(lastDeleted.Uuid);

            Debug.Log($"Restored Oculus anchor with UUID: {lastDeleted.Uuid}");
#endif

#if MAGICLEAP || OCULUS
        // Magic Leap anchors don't require special handling; the prefab is enough
        Debug.Log($"Restored Magic Leap anchor.");
#endif

            // Add the restored anchor back to the anchor data list
            _anchorDataList.Add(new AnchorData
            {
                PrefabName = lastDeleted.PrefabName,
                Prefab = restoredGameObject,
                Position = lastDeleted.Position,
                Rotation = lastDeleted.Rotation,
                Uuid = lastDeleted.Uuid
            });

            // Save the updated anchor data to file
            SaveAnchorDataToFile();

            Debug.Log("Last deleted anchor restored.");
        }
        else
        {
            Debug.Log("No anchors to undo.");
        }
    }



    /******************* Clear All Anchors from Scene *****************/
    private void ClearAllAnchorsFromScene()
    {
#if OCULUSX
        // Clear Oculus anchors
        foreach (var anchor in _anchorInstances)
        {
            if (anchor != null)
            {
                Destroy(anchor.gameObject);
            }
        }

        _anchorInstances.Clear();
        _anchorUuids.Clear(); // Clear the UUID list
        Debug.Log("All Oculus anchors cleared from scene and memory.");
#elif MAGICLEAP|| OCULUS
        // Clear Magic Leap anchors
        foreach (var anchorData in _anchorDataList)
        {
            if (anchorData.Prefab != null)
            {
                Destroy(anchorData.Prefab);
            }
        }

        _anchorDataList.Clear(); // Clear the anchor data list
        Debug.Log("All Magic Leap anchors cleared from scene and memory.");
#endif

        // Save the updated anchor data (empty lists)
        SaveAnchorDataToFile();
    }


}
