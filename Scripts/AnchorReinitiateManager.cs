/*
 * AnchorReinitiateManager.cs
 *
 * Description:
 * This script manages the reinitialization of anchors in a Unity scene using JSON data and user-provided registration cubes.
 * The workflow involves:
 * - Loading anchor data from a JSON file (`anchorDataFilePath`).
 * - Dynamically placing `N` registration cubes at physical markers and aligning them with saved anchor markers.
 * - Applying spatial registration (translation, rotation, scaling) to instantiate anchors at correct locations in the scene.
 *
 * Key Features:
 * 1. Dynamically handles `N` registration cubes and aligns them using Procrustes Analysis.
 * 2. Includes an option to enable or disable scaling.
 * 3. User feedback via a UI text element (`uiInstructions`).
 * 4. Provides error handling for missing data, invalid input, or null references.
 * 
 * /// NOTE ON REGISTRATION MARKER PLACEMENT:
 * /// For a stable and reliable Procrustes-based alignment, your physical markers (and thus the registration cubes)
 * /// must not lie in a straight line or near-collinear arrangement. When points are nearly collinear, the algorithm 
 * /// cannot uniquely determine rotation about the axis defined by those points, often resulting in unexpected 
 * /// orientations. Spreading registration cubes in a more 3D arrangement (e.g., forming a triangle or polygon) 
 * /// provides sufficient constraints to accurately solve for the transformation in all dimensions.
 * 
 * Saving the Data:
 * Check if the ExperimentManager is enabled and has 
 *
 * Author: [Abhinav Azad]
 * Date: [22-12-2024]
 */

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Linq;

// Math.NET Numerics
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Single;
using UnityEngine.UIElements;

using UIToggle = UnityEngine.UI.Toggle;
using UIButton = UnityEngine.UI.Button;
#if MAGICLEAP
using UnityEngine.XR.MagicLeap;
#endif


public class AnchorReinitiateManager : MonoBehaviour
{
    //private ExperimentManager experimentManager;//Not needed but just kept to know that we have referenced another script in this script

    private TextMeshProUGUI DebugText;

    private AnchorUIManager anchorUIManager;
    private List<GameObject> placeablePrefabs;

    [SerializeField] private UIButton reinitiateSceneButton; // UI button to start reinitiation

    [SerializeField] private UIButton registrationButton; // UI button to complete registration

    [SerializeField] private Transform Rcontroller_Transform; // Transform of the right-hand controller

    [SerializeField] private GameObject registrationCubePrefab; // Prefab for small registration cubes
    [SerializeField] private GameObject registrationVertexPrefab; // Prefab for small registration cubes

    [SerializeField] private TextMeshProUGUI uiInstructions; // UI text to display instructions

    [SerializeField] private TMP_InputField fileNameInputField; // Input field for the file name

    [SerializeField] private int RegCount = 4; // Number of registration cubes

    //[SerializeField]
    //private bool enableScaling = false; // Toggle to enable or disable scaling -> replaced by enableScalingToggle.isOn

    [SerializeField] private TMP_InputField RegCountbyUI; // Input field for the file name
    [SerializeField] private UIToggle enableScalingToggle;        // UI toggle for scaling
    [SerializeField] private UIToggle RegCountRegisterToggle;  // UI toggle to register only first RegCounts

    private string fileName = "Anchor_data11";
    private string filePath;// = $"/storage/emulated/0/Documents/FireEvacCivil/Anchors/";
    private string anchorDataFilePath;// = "/storage/emulated/0/Documents/FireEvacCivil/Anchors/Anchor_data.json";

    private List<AnchorData> anchorDataList = new();
    private List<AnchorData> updatedAnchors = new();

    private string registrationJson;
    private string registrationFileName;

    private List<Vector3> markerWorldPositions = new(); // Saved anchor marker positions
    private List<Vector3> registrationCubePositions = new(); // User-placed registration cube positions

    private GameObject[] registrationCubes; // Array for dynamically spawned registration cubes

    private GameObject _currentPlacingObject;
    private int currentStep = 0;


    [Serializable]
    public class AnchorData
    {
        public string PrefabName;  // Name of the prefab
        public GameObject Prefab;  // Reference to the original prefab
        public Vector3 Position;   // Global position
        public Quaternion Rotation; // Global rotation
        public Guid Uuid;         // Anchor UUID
    }

    [Serializable]
    public class RegistrationSaveData
    {
        public AnchorDataListWrapper registrationCubesData;
        public int regCount;
        public bool scalingUsed;
        public ProcrustesResultData alignmentResult;
    }

    [Serializable]
    public class AnchorDataListWrapper
    {
        public List<AnchorData> Anchors;
    }

    [Serializable]
    public class ProcrustesResultData
    {
        // We'll store rotation as x,y,z,w
        public float[] rotation;     // 4-element array for Quaternion
        public float[] translation;  // 3-element array for Vector3
        public float scale;
    }

#if MAGICLEAP
    // Magic Leap inputs to detect if the user is pressing Menu, Bumper, or Trigger.
    private MagicLeapInputs _magicLeapInputs;
    private MagicLeapInputs.ControllerActions _controllerActions;
#endif

    private void OnEnable()
    {
#if OCULUS
        // No explicit input initialization for Oculus
#endif

#if MAGICLEAP
        // Initialize Input for ML
        if (_magicLeapInputs == null)
        {
            _magicLeapInputs = new MagicLeapInputs();
            _controllerActions = new MagicLeapInputs.ControllerActions(_magicLeapInputs);
        }

        _magicLeapInputs.Enable();

        // Bind input actions
        //_controllerActions.Bumper.performed += ctx => UpdatePlacingObject();
        _controllerActions.Bumper.canceled += ctx => FinalizePlacingObject();
#endif

    }

    private void OnDisable()
    {
#if MAGICLEAP
        // Unsubscribe from controller input when the object is disabled
        if (_magicLeapInputs != null)
        {
            // UnBind input actions
            //_controllerActions.Bumper.performed -= ctx => UpdatePlacingObject();
            _controllerActions.Bumper.canceled -= ctx => FinalizePlacingObject();
            _magicLeapInputs.Disable();
        }
#endif
    }

    private void Start()
    {
        registrationCubes = new GameObject[RegCount];

        if (fileNameInputField != null)
        {
            fileNameInputField.text = fileName;
            fileNameInputField.onValueChanged.AddListener(UpdateFileName);
        }

        // Initialize RegCountbyUI input
        if (RegCountbyUI != null)
        {
            RegCountbyUI.text = RegCount.ToString();
            RegCountbyUI.onValueChanged.AddListener(ConvertInputToInteger); //Updates RegCount if there is an input
        }

        anchorUIManager = FindObjectOfType<AnchorUIManager>();
        placeablePrefabs = anchorUIManager?.PlaceablePrefabs;
        filePath = anchorUIManager?.AnchorDataFilePathRef;  //$"{Application.persistentDataPath}/AnchorsData/";
        if (filePath == null)
        {
            filePath = $"{Application.persistentDataPath}/AnchorsData/";
        }


        if (anchorUIManager == null || placeablePrefabs == null || placeablePrefabs.Count == 0)
        {
            Debug.LogError("AnchorUIManager or placeablePrefabs is not properly initialized!");
            return;
        }

        if (reinitiateSceneButton != null)
            reinitiateSceneButton.onClick.AddListener(StartReinitiation);

        if (registrationButton != null)
            registrationButton.onClick.AddListener(CompleteRegistration);

        if (enableScalingToggle != null)
        {
            // 1. Set default to false (unchecked) so scaling is off initially
            enableScalingToggle.isOn = false;

            // 2. Add a listener that runs whenever the Toggle is changed
            enableScalingToggle.onValueChanged.AddListener((bool isOn) =>
            {
                // Here, we can either just rely on 'enableScalingToggle.isOn' everywhere,
                // or store the current value in a private bool. For example:

                // If you want to store it in a private bool:
                // enableScaling = isOn;

                // Alternatively, you can skip storing it and 
                // just use `enableScalingToggle.isOn` in your code wherever needed.

                Debug.Log($"Scaling toggled to: {isOn}");
            });
        }

    }

    private void UpdateFileName(string newName)
    {
        //This function only updates the fileName based on changes in fileNameInputField
        fileName = newName; // Update the base name in real-time
        Debug.Log($"File name updated to: {fileName}");
    }

    private void Update()
    {
        anchorDataFilePath = Path.Combine(filePath, fileName + ".json");

        // If we are in the process of placing registration cubes
        if (currentStep > 0 && currentStep <= RegCount && _currentPlacingObject != null)
        {
            UpdatePlacingObject();

            // Example: OVRInput.Button.SecondaryHandTrigger for "placing" an object
#if OCULUS
            if (OVRInput.GetUp(OVRInput.Button.SecondaryHandTrigger))
            {
                FinalizePlacingObject();
            }
#endif
        }
    }

    private void StartPlacingObject(GameObject prefab, Transform referenceTransform)
    {
        _currentPlacingObject = Instantiate(prefab, referenceTransform.position, Quaternion.identity);
        DebugText.text = $"currentStep: {currentStep}, CurrentObj: {_currentPlacingObject.name}";
    }

    private void UpdatePlacingObject()
    {
        if (_currentPlacingObject != null && Rcontroller_Transform != null)
        {
            _currentPlacingObject.transform.position = Rcontroller_Transform.position;
            var rotation = Rcontroller_Transform.rotation.eulerAngles;
            _currentPlacingObject.transform.rotation = Quaternion.Euler(0, rotation.y, 0);
        }
    }

    private void FinalizePlacingObject()
    {
        if (_currentPlacingObject != null)
        {
            registrationCubes[currentStep - 1] = _currentPlacingObject;
            registrationCubePositions.Add(_currentPlacingObject.transform.position);
            _currentPlacingObject = null;
            currentStep++;

            if (currentStep <= RegCount)
            {
                // Continue to Spwan the next registration cube using StartPlacingObject()
                SpawnRegistrationCube();
            }
        }
    }

    private void SpawnRegistrationCube()
    {
        if (RegCount == 1)
        {
            StartPlacingObject(registrationVertexPrefab, Rcontroller_Transform);
        }
        else
        {
            StartPlacingObject(registrationCubePrefab, Rcontroller_Transform);

        }
        UpdateUI($"Place registration cube {currentStep} at marker {currentStep}.");
    }

    private void StartReinitiation()
    {
        registrationCubes = new GameObject[RegCount];

        if (!File.Exists(anchorDataFilePath))
        {
            UpdateUI("No saved anchors found.");
            return;
        }

        var jsonData = File.ReadAllText(anchorDataFilePath);
        var anchorDataWrapper = JsonUtility.FromJson<AnchorDataListWrapper>(jsonData);
        anchorDataList = anchorDataWrapper?.Anchors;

        if (anchorDataList == null || anchorDataList.Count < RegCount)
        {
            UpdateUI("Not enough anchors saved for registration.");
            return;
        }

        // Collect the first RegCount anchor positions
        markerWorldPositions.Clear();
        for (int i = 0; i < RegCount; i++)
        {
            markerWorldPositions.Add(anchorDataList[i].Position);
        }

        // Clear existing registration data and start placing new cubes
        currentStep = 1;
        registrationCubePositions.Clear();
        SpawnRegistrationCube();
    }
    private void CompleteRegistration()
    {
        // Fresh start updatedAnchors for evert new registration.
        updatedAnchors = new List<AnchorData>();

        // Ensure we have the correct number of placed cubes
        if (registrationCubePositions.Count != RegCount || markerWorldPositions.Count != RegCount)
        {
            UpdateUI("All registration cubes must be placed before completing registration.");
            return;
        }

        //initialising scale
        float scale = 1f;

        if (RegCount == 1) //
        {
            // Get the first registration cube position and rotation
            Vector3 regCubePosition = registrationCubes[0].transform.position;
            Quaternion regCubeRotation = registrationCubes[0].transform.rotation;

            // Get the first anchor data
            var firstAnchor = anchorDataList[0];

            // Compute the offset between the saved anchor and the first registration cube
            Vector3 offsetPosition = regCubePosition - firstAnchor.Position;
            Quaternion offsetRotation = regCubeRotation * Quaternion.Inverse(firstAnchor.Rotation);


            int iterationCount = 0;
            // Apply calculated transform to each anchor and instantiate in scene
            foreach (var anchorData in anchorDataList)
            {

                if (iterationCount >= RegCount && RegCountRegisterToggle.isOn == true)
                {
                    Debug.Log("Reached maximum iterations allowed by RegCount.");
                    break;
                }

                // Calculate the adjusted position and rotation
                Vector3 relativePosition = anchorData.Position - firstAnchor.Position;
                Vector3 adjustedPosition = regCubePosition + offsetRotation * relativePosition;

                Quaternion relativeRotation = Quaternion.Inverse(firstAnchor.Rotation) * anchorData.Rotation;
                Quaternion adjustedRotation = regCubeRotation * relativeRotation;


                // Added: Save updated data to the list
                updatedAnchors.Add(new AnchorData
                {
                    PrefabName = anchorData.PrefabName,
                    Prefab = anchorData.Prefab, // Optional for scene instantiation
                    Position = adjustedPosition,
                    Rotation = adjustedRotation,
                    Uuid = anchorData.Uuid
                });

                // Instantiate prefab at adjusted position and rotation
                var prefab = placeablePrefabs.Find(p => p.name == anchorData.PrefabName);
                if (prefab != null)
                {
                    Instantiate(prefab, adjustedPosition, adjustedRotation);
                }
                else
                {
                    Debug.LogWarning($"Prefab not found for anchor: {anchorData.PrefabName}, using default cube...");
                    //Instantiate(GameObject.CreatePrimitive(PrimitiveType.Cube), adjustedPosition, adjustedRotation);

                }
                iterationCount++; // Increment the counter

            }
        }
        else
        {
            // Perform Procrustes Analysis
            var (rotation, translationNotused, scalea, markerCentroid, cubeCentroid) =
                ProcrustesAnalysis(markerWorldPositions, registrationCubePositions, enableScalingToggle.isOn);
            scale = scalea;

            int iterationCount = 0;
            // Apply calculated transform to each anchor and instantiate in scene
            foreach (var anchorData in anchorDataList)
            {

                if (iterationCount >= RegCount && RegCountRegisterToggle.isOn == true)
                {
                    Debug.Log("Reached maximum iterations allowed by RegCount.");
                    break;
                }


                // Shift anchor position so its centroid is at the origin
                Vector3 anchorRelative = anchorData.Position - markerCentroid;

                // Rotate and scale if scaling is enabled
                if (enableScalingToggle.isOn) anchorRelative *= scalea;
                anchorRelative = rotation * anchorRelative;

                // Finally translate to the new centroid
                Vector3 adjustedPosition = anchorRelative + cubeCentroid;

                // Adjusted rotation
                Quaternion adjustedRotation = rotation * anchorData.Rotation;


                // Added: Save updated data to the list
                updatedAnchors.Add(new AnchorData
                {
                    PrefabName = anchorData.PrefabName,
                    Prefab = anchorData.Prefab, // Optional for scene instantiation
                    Position = adjustedPosition,
                    Rotation = adjustedRotation,
                    Uuid = anchorData.Uuid
                });

                // Instantiate prefab at adjusted position and rotation
                var prefab = placeablePrefabs.Find(p => p.name == anchorData.PrefabName);
                if (prefab != null)
                {
                    Instantiate(prefab, adjustedPosition, adjustedRotation);
                }
                else
                {
                    Debug.LogWarning($"Prefab not found for anchor: {anchorData.PrefabName}, using default cube...");
                    //Instantiate(GameObject.CreatePrimitive(PrimitiveType.Cube), adjustedPosition, adjustedRotation);

                }
                iterationCount++; // Increment the counter

            }
        }



        // ---------------------
        // 1) Prepare to save data in the experiment folder
        // ---------------------
        string experimentFolder;

        //var experimentManager = FindObjectOfType<ExperimentManager>();
        if (ExperimentManager.filePath == null)
        {
            Debug.LogError("No ExperimentManager found in scene!");
            UpdateUI("No ExperimentManager found in scene!");
            experimentFolder = $"{Application.persistentDataPath}/FireEvacCivil/{DateTime.Now:yyyy-MM-dd}/Reg_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}/"; ; //Just incase experimentManager is not running and hence Null
            Debug.Log($"ExperimentManager not found so default experimentFolder created at: {experimentFolder}");
            //return;
        }
        else
        {
            experimentFolder = ExperimentManager.filePath;// this will ensure that the experimentFolder is same as the folder used by ExperimentManager.cs to store data
            if (string.IsNullOrEmpty(experimentFolder))
            {
                Debug.LogError("Experiment folder path is invalid!");
                UpdateUI("Experiment folder path is invalid!");
                return;
            }
        }
        //Create experimentFolder if already doesnt exists
        Directory.CreateDirectory(experimentFolder);

        // ---------------------
        // 1. Save the Sanity check anchor data (with suffix) before Registration
        string anchorsSavePath = Path.Combine(experimentFolder, $"{ButtonActivation.XRMode}_{fileName}_{!RegCountRegisterToggle.isOn}_RegAll_BeforeReg.json");
        SaveUpdatedAnchors(anchorDataList, anchorsSavePath);
        Debug.Log("Saved Sanity anchors data with suffix at: " + anchorsSavePath);


        // 2. Save updated anchors as JSON after Registration
        string UpdatedanchorsSavePath = Path.Combine(experimentFolder, $"{ButtonActivation.XRMode}_{fileName}_{!RegCountRegisterToggle.isOn}_RegAll_PostReg.json");
        SaveUpdatedAnchors(updatedAnchors, UpdatedanchorsSavePath);

        // ---------------------
        // 3) Save registration data (the cubes placed, RegCount, scale bool, AND Procrustes result)
        // ---------------------
        var registrationAnchors = new List<AnchorData>();
        for (int i = 0; i < registrationCubes.Length; i++)
        {
            var cube = registrationCubes[i];
            if (cube != null)
            {
                registrationAnchors.Add(
                    new AnchorData
                    {
                        PrefabName = "RegistrationCube",  // or your chosen name
                        Prefab = null,                   // typically not needed
                        Position = cube.transform.position,
                        Rotation = cube.transform.rotation,
                        Uuid = Guid.NewGuid()
                    }
                );
            }
        }

        // Create the ProcrustesResultData
        // We'll store the quaternion as x,y,z,w and translation as x,y,z.
        ProcrustesResultData alignmentRes = new ProcrustesResultData { };

        var regDataWrapper = new RegistrationSaveData
        {
            registrationCubesData = new AnchorDataListWrapper { Anchors = registrationAnchors },
            regCount = RegCount,
            scalingUsed = enableScalingToggle.isOn,

            // Assign the Procrustes result
            alignmentResult = alignmentRes
        };

        registrationJson = JsonUtility.ToJson(regDataWrapper, true);
        registrationFileName = $"{ButtonActivation.XRMode}_RegistrationData_{!RegCountRegisterToggle.isOn}_RegAll_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";

        string registrationSavePath = Path.Combine(experimentFolder, registrationFileName);
        File.WriteAllText(registrationSavePath, registrationJson);
        Debug.Log("Saved registration data at: " + registrationSavePath);

        // Generate a random color
        Color randomColor = new Color(
            0.2f, // Random red value (0 to 1)
            UnityEngine.Random.value, // Random green value (0 to 1)
            UnityEngine.Random.value  // Random blue value (0 to 1)
        );

        // Uncomment if you dont want to remove registration cubes from the scene after alignment:
        foreach (var cube in registrationCubes)
        {
            Renderer cubeRenderer = cube.GetComponent<Renderer>();
            cubeRenderer.material.color = randomColor;
            //if (cube != null) Destroy(cube);
        }

        UpdateUI($"Scene successfully reinitialized with Scaling {enableScalingToggle.isOn} = {scale}");
        Debug.Log($"Scene successfully reinitialized with Scaling {enableScalingToggle.isOn} = {scale}");
        currentStep = 0;
    }


    /// <summary>
    /// Performs a Procrustes alignment from the list of marker positions (source)
    /// to the list of registration cubes (target). Returns (R, T, scale, centroidMarkers, centroidCubes).
    /// 
    /// If enableScale == false, scale is forced to 1.0f.
    /// </summary>
    private (Quaternion rotation, Vector3 translation, float scale, Vector3 markerCentroid, Vector3 cubeCentroid)
        ProcrustesAnalysis(List<Vector3> markers, List<Vector3> cubes, bool enableScale)
    {
        if (markers.Count != cubes.Count)
        {
            Debug.LogError("Markers and cubes count mismatch.");
            return (Quaternion.identity, Vector3.zero, 1f, Vector3.zero, Vector3.zero);
        }

        int n = markers.Count;

        // Convert to Math.NET matrices
        var markerMatrix = DenseMatrix.OfRows(n, 3, markers.Select(v => new float[] { v.x, v.y, v.z }));
        var cubeMatrix = DenseMatrix.OfRows(n, 3, cubes.Select(v => new float[] { v.x, v.y, v.z }));

        // Calculate centroids
        var markerCentroidVec = markerMatrix.ColumnSums() / n;
        var cubeCentroidVec = cubeMatrix.ColumnSums() / n;

        // Center the points around their centroids
        for (int i = 0; i < n; i++)
        {
            markerMatrix.SetRow(i, markerMatrix.Row(i) - markerCentroidVec);
            cubeMatrix.SetRow(i, cubeMatrix.Row(i) - cubeCentroidVec);
        }

        // Compute covariance matrix: M^T * C  (if we want to go Marker->Cube, typically do marker^T * cube)
        var covariance = markerMatrix.TransposeThisAndMultiply(cubeMatrix);

        // Perform SVD
        var svd = covariance.Svd();
        // Construct rotation: R = V * U^T
        // Because we did marker^T * cube, the typical formula for rotation is R = U * V^T; 
        // but note the direction.  We'll keep to the standard approach below:
        var U = svd.U;
        var VT = svd.VT;
        var rotationMatrix = U * VT;

        // Fix for reflection case (if det(R) < 0, flip sign of last column)
        if (rotationMatrix.Determinant() < 0)
        {
            U.SetColumn(U.ColumnCount - 1, U.Column(U.ColumnCount - 1).Multiply(-1));
            rotationMatrix = U * VT;
        }

        // Convert to Unity Quaternion
        Quaternion rotation = MatrixToQuaternion(rotationMatrix);

        // Calculate scale in a more standard Procrustes manner:
        float scale = 1f;
        if (enableScale)
        {
            // numerator = sum of singular values
            float sumS = 0f;
            foreach (var val in svd.S.ToArray()) sumS += val;

            // denominator = sum of squared distances in markerMatrix
            // i.e. the Frobenius norm squared
            float sumOfSquares = 0f;
            for (int i = 0; i < n; i++)
            {
                var row = markerMatrix.Row(i);
                sumOfSquares += row[0] * row[0] + row[1] * row[1] + row[2] * row[2];
            }

            // scale = sumS / sumOfSquares
            // If sumOfSquares is near zero, we clamp to avoid dividing by zero
            if (Mathf.Abs(sumOfSquares) > 1e-6f)
            {
                scale = sumS / sumOfSquares;
            }
            else
            {
                scale = 1f;
                Debug.LogWarning("Markers are nearly coincident; scale set to 1.");
            }
        }

        // Convert centroids to Unity Vector3
        Vector3 markerCentroidUnity = new Vector3(markerCentroidVec[0], markerCentroidVec[1], markerCentroidVec[2]);
        Vector3 cubeCentroidUnity = new Vector3(cubeCentroidVec[0], cubeCentroidVec[1], cubeCentroidVec[2]);

        // The final translation from marker space -> cube space
        // T = cCubes - s * R * cMarkers
        Vector3 translation = cubeCentroidUnity - rotation * (markerCentroidUnity * scale);

        return (rotation, translation, scale, markerCentroidUnity, cubeCentroidUnity);
    }

    // Helper method to convert a 3x3 rotation matrix to a Unity Quaternion
    private Quaternion MatrixToQuaternion(Matrix<float> rMat)
    {
        // rMat is 3x3
        float trace = rMat[0, 0] + rMat[1, 1] + rMat[2, 2];
        Quaternion q = new Quaternion();

        if (trace > 0)
        {
            float s = 0.5f / Mathf.Sqrt(trace + 1.0f);
            q.w = 0.25f / s;
            q.x = (rMat[2, 1] - rMat[1, 2]) * s;
            q.y = (rMat[0, 2] - rMat[2, 0]) * s;
            q.z = (rMat[1, 0] - rMat[0, 1]) * s;
        }
        else
        {
            if (rMat[0, 0] > rMat[1, 1] && rMat[0, 0] > rMat[2, 2])
            {
                float s = 2.0f * Mathf.Sqrt(1.0f + rMat[0, 0] - rMat[1, 1] - rMat[2, 2]);
                q.w = (rMat[2, 1] - rMat[1, 2]) / s;
                q.x = 0.25f * s;
                q.y = (rMat[0, 1] + rMat[1, 0]) / s;
                q.z = (rMat[0, 2] + rMat[2, 0]) / s;
            }
            else if (rMat[1, 1] > rMat[2, 2])
            {
                float s = 2.0f * Mathf.Sqrt(1.0f + rMat[1, 1] - rMat[0, 0] - rMat[2, 2]);
                q.w = (rMat[0, 2] - rMat[2, 0]) / s;
                q.x = (rMat[0, 1] + rMat[1, 0]) / s;
                q.y = 0.25f * s;
                q.z = (rMat[1, 2] + rMat[2, 1]) / s;
            }
            else
            {
                float s = 2.0f * Mathf.Sqrt(1.0f + rMat[2, 2] - rMat[0, 0] - rMat[1, 1]);
                q.w = (rMat[1, 0] - rMat[0, 1]) / s;
                q.x = (rMat[0, 2] + rMat[2, 0]) / s;
                q.y = (rMat[1, 2] + rMat[2, 1]) / s;
                q.z = 0.25f * s;
            }
        }

        return q.normalized;
    }

    // Function to convert input field string to an integer
    private void ConvertInputToInteger(string input)
    {
        int inputValue;
        // Try to parse the input string to an integer
        if (int.TryParse(input, out inputValue))
        {
            RegCount = inputValue;
            Debug.Log($"Successfully converted input to integer: {inputValue}");
            // You can now use inputValue as an integer
        }
        else
        {
            Debug.LogWarning("Invalid input! Please enter a valid integer.");
        }

    }

    private void SaveUpdatedAnchors(List<AnchorData> updatedAnchors, string savePath)
    {
        // Create a wrapper for serialization
        var anchorDataWrapper = new AnchorDataListWrapper { Anchors = updatedAnchors };

        // Convert to JSON
        string updatedAnchorJson = JsonUtility.ToJson(anchorDataWrapper, true);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(savePath)); // Ensure directory of the last file exists
            File.WriteAllText(savePath, updatedAnchorJson);
            Debug.Log($"Updated anchor data saved at: {savePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to save updated anchor data: {ex.Message}");
        }
    }
    public void saveFileatStart()
    {
        if (string.IsNullOrEmpty(registrationFileName))
        {
            Debug.Log("registrationFileName is not initialized. Aborting saveFileatStart().");
            Debug.LogError("registrationFileName is not initialized. Aborting saveFileatStart().");
            return;
        }

        string experimentFolder = ExperimentManager.filePath;// this will ensure that the experimentFolder is same as the folder used by ExperimentManager.cs to store data
        // 1. Save the Sanity check anchor data (with suffix) before Registration
        string anchorsSavePath = Path.Combine(experimentFolder, $"{ButtonActivation.XRMode}_{fileName}_{!RegCountRegisterToggle.isOn}_RegAll_BeforeRegAnchors.json");
        SaveUpdatedAnchors(anchorDataList, anchorsSavePath);
        Debug.Log("Saved Sanity anchors data with suffix at: " + anchorsSavePath);


        // 2. Save updated anchors as JSON after Registration
        string UpdatedanchorsSavePath = Path.Combine(experimentFolder, $"{ButtonActivation.XRMode}_{fileName}_{!RegCountRegisterToggle.isOn}_RegAll_PostRegAnchors.json");
        SaveUpdatedAnchors(updatedAnchors, UpdatedanchorsSavePath);

        string registrationSavePath = Path.Combine(experimentFolder, registrationFileName);
        File.WriteAllText(registrationSavePath, registrationJson);
        Debug.Log("Saved registration data at: " + registrationSavePath);
    }

    private void UpdateUI(string message)
    {
        if (uiInstructions != null)
            uiInstructions.text = message;

        Debug.Log(message);
    }
}
