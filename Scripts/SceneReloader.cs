/// <summary>
/// SceneReloader: A Unity script to reload a scene using a JSON file.
/// 
/// Key Features:
/// - Reads a JSON file containing anchor data (positions, rotations, and prefab names).
/// - Instantiates prefabs at the specified positions and rotations as children of a designated parent GameObject.
/// - Fallback to instantiate a default pink cube if a required prefab is not found.
/// - Allows saving the positions and rotations of all child objects of the parent GameObject back to a JSON file.
/// 
/// Usage:
/// 1. Drag and drop the required JSON file into the "JSON File" field in the Inspector.
/// 2. Assign the parent GameObject under which prefabs should be instantiated.
/// 3. Add prefabs to the "Prefabs" list for matching with the JSON file.
/// 4. Use the context menu options "Reload Scene" and "Save Scene" to perform respective actions.
/// 
/// Notes:
/// - The JSON file must be structured according to the specified format.
/// - If a prefab is not found, a pink cube will be instantiated as a placeholder.
/// </summary>


using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class SceneReloader : MonoBehaviour
{
    [Serializable]
    public class PrefabData
    {
        public int instanceID; // Not actively used in this implementation
    }

    [Serializable]
    public class PositionData
    {
        public float x, y, z;
    }

    [Serializable]
    public class RotationData
    {
        public float x, y, z, w;
    }

    [Serializable]
    public class AnchorData
    {
        public string PrefabName;
        public PrefabData Prefab;
        public PositionData Position;
        public RotationData Rotation;
    }

    [Serializable]
    public class AnchorsData
    {
        public AnchorData[] Anchors;
    }

    [Header("Prefabs")]
    [Tooltip("List of prefabs available for instantiation.")]
    public GameObject[] prefabs;

    [Header("JSON File")]
    [Tooltip("Drag and drop the JSON file here.")]
    public TextAsset jsonFile;

    [Header("Parent Object")]
    [Tooltip("Parent GameObject under which all prefabs will be instantiated.")]
    public GameObject parentObject;

    private AnchorsData anchorsData;

    // ...

    // [ADDED] We'll store anchor0’s original transform from the loaded JSON.

    [Header("Realignment")]
    [Tooltip("Decide weather to realign 1st Anchor as orign")]
    public bool ShiftAnchor0ToOrigin = false;
    public Vector3 anchor0Pos;    // Original position of anchor0
    public Quaternion anchor0Rot; // Original rotation of anchor0

    // ...

    /// <summary>
    /// Reloads the scene by reading the JSON file and instantiating prefabs as children of the parentObject.
    /// </summary>
    [ContextMenu("Reload Scene")]
    public void ReloadScene()
    {
        //ShiftAnchor0ToOrigin = false;
        if (jsonFile == null)
        {
            Debug.LogError("[SceneReloader] JSON file is not assigned. Please drag and drop a JSON file in the Inspector.");
            return;
        }

        // Validate parent object
        if (parentObject == null)
        {
            Debug.LogError("[SceneReloader] Parent GameObject is not assigned.");
            return;
        }

        // Load and parse the JSON file
        LoadJsonFile();

        // Instantiate prefabs based on parsed JSON data
        InstantiatePrefabsAsChildren();
    }

    /// <summary>
    /// Saves all child objects of the parentObject to a JSON file with "_edit" suffix.
    /// </summary>
    [ContextMenu("Save Scene")]
    public void SaveScene()
    {
        LoadJsonFile(); // Call LoadJsonFile() bedore SaveScene, to reload the latest choice anchor0Pos and anchor0Rot, incase the origin choice might have changed which may depend on the Anchor Data file, which could have been changed as well.

        // Validate parent object
        if (parentObject == null)
        {
            Debug.LogError("[SceneReloader] Parent GameObject is not assigned.");
            return;
        }

        // Get all child objects of the parentObject
        // Get only the direct child objects of the parentObject 
        Transform[] childTransforms = parentObject.transform.Cast<Transform>().ToArray(); //[CHANGED] .Cast<Transform>() retrieves only the direct children of a parent GameObject.
        int childCount = childTransforms.Length; // [CHANGED]

        if (childCount <= 0)
        {
            Debug.LogWarning("[SceneReloader] No child objects found under the parent object.");
            return;
        }

        // Create a new AnchorsData object
        AnchorsData newAnchorsData = new AnchorsData
        {
            Anchors = new AnchorData[childCount]
        };

        // Populate AnchorsData with child object details
        int index = 0;
        foreach (Transform child in childTransforms)
        {
            if (child == parentObject.transform) continue; // Skip the parent object itself

            // Transformation to realign anchors to their original coordinate frame
            // [CHANGED/ADDED] The child's localPosition/Rotation is in the "anchor0=origin" frame.
            // We revert it back to the original coordinate system from the JSON.
            Vector3 shiftedPos = child.position;   // current local pos (anchor0 shifted)
            Quaternion shiftedRot = child.rotation; // current local rot (anchor0 shifted)

            // Recompute original position/orientation
            Vector3 originalPos = anchor0Pos + anchor0Rot * shiftedPos;
            Quaternion originalRot = anchor0Rot * shiftedRot;

            AnchorData anchor = new AnchorData
            {
                PrefabName = child.name,
                Position = new PositionData { x = originalPos.x, y = originalPos.y, z = originalPos.z },
                Rotation = new RotationData { x = originalRot.x, y = originalRot.y, z = originalRot.z, w = originalRot.w }
            };

            newAnchorsData.Anchors[index++] = anchor;
        }

#if UNITY_EDITOR
        string outputFilePath = GetEditedFilePath(AssetDatabase.GetAssetPath(jsonFile));
#else
        string outputFilePath = $"{Application.persistentDataPath}/FireEvacCivil/";
    Debug.LogError("[SceneReloader] Saving scene is only supported in the Unity Editor.");
    return;
#endif
        try
        {
            string jsonContent = JsonUtility.ToJson(newAnchorsData, true);
            File.WriteAllText(outputFilePath, jsonContent);
            Debug.Log($"[SceneReloader] Scene saved successfully to: {outputFilePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SceneReloader] Failed to save JSON file: {ex.Message}");
        }
    }

    /// <summary>
    /// Constructs the edited file path by adding "_edit" before the file extension.
    /// </summary>
    /// <param name="filePath">Original file path.</param>
    /// <returns>Edited file path.</returns>
    private string GetEditedFilePath(string filePath)
    {
        string directory = Path.GetDirectoryName(filePath);
        string filenameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        string extension = Path.GetExtension(filePath);
        return Path.Combine(directory, $"edit_ReOrigin{ShiftAnchor0ToOrigin}_{filenameWithoutExtension}_{extension}");
    }

    /// <summary>
    /// Loads and parses the JSON file into anchorsData.
    /// </summary>
    private bool LoadJsonFile()
    {
        if (jsonFile == null)
        {
            Debug.LogError("[SceneReloader] JSON file is not assigned. Please drag and drop a JSON file in the Inspector.");
            return false;
        }

        try
        {
            string jsonContent = jsonFile.text; // [CHANGED] Access the text content of the TextAsset
            anchorsData = JsonUtility.FromJson<AnchorsData>(jsonContent);

            if (anchorsData?.Anchors == null || anchorsData.Anchors.Length == 0)
            {
                Debug.LogError("[SceneReloader] JSON parsed but contains no anchors.");
                return false;
            }


            // [ADDED] Capture the first anchor's original transform for reference
            if (ShiftAnchor0ToOrigin == false)
            {
                // anchor0 initialised such that it doesnt make any changes during the transformation
                anchor0Pos = Vector3.zero; // Initializes to (0, 0, 0)
                anchor0Rot = Quaternion.identity; // Initializes to no rotation (0, 0, 0, 1)
            }


            if (ShiftAnchor0ToOrigin == true)
            {
                AnchorData anchor0 = anchorsData.Anchors[0];
                anchor0Pos = new Vector3(anchor0.Position.x, anchor0.Position.y, anchor0.Position.z);
                anchor0Rot = new Quaternion(anchor0.Rotation.x, anchor0.Rotation.y, anchor0.Rotation.z, anchor0.Rotation.w);
            }


            Debug.Log($"[SceneReloader] Successfully loaded {anchorsData.Anchors.Length} anchors from JSON.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SceneReloader] Failed to parse JSON file: {ex.Message}");
            return false;
        }
    }


    /// <summary>
    /// Instantiates prefabs as children of the designated parent object.
    /// </summary>
    private void InstantiatePrefabsAsChildren()
    {
        foreach (var anchor in anchorsData.Anchors)
        {
            // [CHANGED/ADDED] Convert from the original anchor transform to the new "anchor0 at origin" transform
            Vector3 oldPos = new Vector3(anchor.Position.x, anchor.Position.y, anchor.Position.z);
            Quaternion oldRot = new Quaternion(anchor.Rotation.x, anchor.Rotation.y, anchor.Rotation.z, anchor.Rotation.w);

            // Relative Transformations to realign 1st anchors to origin
            // Shift anchor0 to (0,0,0) with identity rotation
            Vector3 shiftedPos = oldPos - anchor0Pos;                      // remove anchor0's old position
            shiftedPos = Quaternion.Inverse(anchor0Rot) * shiftedPos;      // rotate by the inverse of anchor0's old rotation

            Quaternion shiftedRot = Quaternion.Inverse(anchor0Rot) * oldRot;

            GameObject prefab = FindPrefabByName(anchor.PrefabName);
            if (prefab == null)
            {
                Debug.LogWarning($"[SceneReloader] Prefab '{anchor.PrefabName}' not found. Instantiating a pink cube instead."); // [CHANGED]

                // Instantiate a simple pink cube if prefab not found [CHANGED]
                prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            }


            // Instantiate in the "shifted" frame
            GameObject instance = Instantiate(prefab, shiftedPos, shiftedRot, parentObject.transform);
            instance.name = anchor.PrefabName;

            Debug.Log($"[SceneReloader] Instantiated '{anchor.PrefabName}' as a child of '{parentObject.name}' at position {shiftedPos} with rotation {shiftedRot}.");
        }
    }

    /// <summary>
    /// Finds a prefab by name in the prefabs array.
    /// </summary>
    private GameObject FindPrefabByName(string prefabName)
    {
        foreach (var prefab in prefabs)
        {
            if (prefab != null && prefab.name == prefabName)
            {
                return prefab;
            }
        }
        return null;
    }

    /// <summary>
    /// Creates a simple pink cube GameObject. [CHANGED]
    /// </summary>
    /// <returns>The created pink cube GameObject.</returns>
    private GameObject CreatePinkCube()
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

        // Create a pink material
        Material pinkMaterial = new Material(Shader.Find("Standard"));
        pinkMaterial.color = Color.magenta; // Pink color

        // Assign the pink material to the cube
        MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
        renderer.material = pinkMaterial;

        return cube;
    }


}
