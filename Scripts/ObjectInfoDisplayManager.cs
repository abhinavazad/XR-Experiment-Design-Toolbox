/*
 * ObjectInfoDisplayManager
 * -------------------------
 * Summary:
 * This script allows the display of position (X, Y, Z) and rotation (X, Y, Z) information over all active 
 * GameObjects in the scene. Each GameObject has its position and rotation separately displayed as 
 * 3D TextMeshPro objects positioned above the object. The feature can be toggled on/off using a keypress 
 * (default: 'T') or programmatically.

 * Features:
 * - Displays X, Y, Z position and rotation of all GameObjects in the scene.
 * - Instantiates separate GameObjects(at Objects Orign for sanity check) and TextMeshPro objects (at a given offset) for position and rotation of the objects.
 * - Updates text dynamically in real-time as objects move or rotate.
 * - Ensures the text always faces the main camera for readability. The text objects are rotated to face the camera using Quaternion.LookRotation.
 * - Allows toggling of the display feature with a keypress or programmatically.

 * Key Functions:
 * - `ToggleDisplayInfo`: Enables/disables the display of information.
 * - `EnableInfoDisplay`: Instantiates TextMeshPro objects and positions them above each GameObject.
 * - `DisableInfoDisplay`: Destroys all instantiated text objects and clears the tracking dictionary.
 * - `UpdateTextPositions`: Continuously updates position and rotation values and ensures the text faces the camera.
 * - `SceneObjects`: Gathers all active GameObjects in the scene for processing.

 * Usage:
 * - Attach this script to a GameObject in your scene.
 * - Assign a TextMeshPro prefab to the `textPrefab` field in the Inspector.
 * - Toggle the display with the 'T' key or call the `ToggleDisplayInfo` function programmatically.
 * - Ensure the TextMeshPro prefab has a readable font size and is suitable for 3D placement.

 * Customization:
 * - Adjust the vertical offsets (`localPosition`) of the position and rotation texts to fit your scene layout.
 * - Modify the `SceneObjects` method to filter objects by tag, layer, or custom criteria if needed.
 * 
 * TAG Usage: 
 * To Display Object info. To ensure only intended objects info is displayed as there are so many invible objects in the background, that whose position/rotation info are not needed to be displayed 

 * Notes:
 * - The script dynamically instantiates and destroys objects, so use with consideration for performance in large scenes.
 */

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObjectInfoDisplayManager : MonoBehaviour
{
    [Header("Settings")]
    public GameObject ObjCentrePrefab; // Assign a prefab for objects at the origin
    public GameObject textPrefab; // Assign a TextMeshPro prefab
    public Toggle displayInfoToggle; // UI Toggle for enabling/disabling display

    [Header("Tag Filtering")]
    public List<string> targetTags = new List<string> { "FireHazard", "Start", "Finish1", "Finish2", "Sign", "RegistrationObjects" }; // Pre-initialized tags

    private Dictionary<GameObject, GameObject[]> textObjects = new(); // 0: PositionText, 1: RotationText, 2: AdditionalObject

    [SerializeField]
    private float PosTextoffset = 0.15f;
    [SerializeField]
    private float RotTextoffset = 0.12f;


    void Start()
    {
        ValidateTags();
        if (textPrefab == null)
        {
            Debug.LogError("Text Prefab is not assigned!");
            return;
        }

        if (displayInfoToggle == null)
        {
            Debug.LogError("Display Info Toggle is not assigned!");
            return;
        }

        // Add listener to toggle's value change
        displayInfoToggle.onValueChanged.AddListener(OnToggleValueChanged);
    }

    void Update()
    {
        if (displayInfoToggle.isOn)
        {
            UpdateTextPositions();
        }
    }

    private void OnToggleValueChanged(bool isOn)
    {
        if (isOn)
        {
            EnableInfoDisplay();
        }
        else
        {
            DisableInfoDisplay();
        }
    }

    private void EnableInfoDisplay()
    {
        foreach (GameObject obj in SceneObjects())
        {
            if (!textObjects.ContainsKey(obj))
            {
                // Instantiate position and rotation text prefabs
                GameObject positionText = Instantiate(textPrefab);
                positionText.transform.position = obj.transform.position + Vector3.up * PosTextoffset; // Position above the object
                positionText.transform.localScale = textPrefab.transform.localScale; // Ensure global scale

                GameObject rotationText = Instantiate(textPrefab);
                rotationText.transform.position = obj.transform.position + Vector3.up * RotTextoffset; // Slightly below the position text
                rotationText.transform.localScale = textPrefab.transform.localScale; // Ensure global scale

                TextMeshPro positionMesh = positionText.GetComponent<TextMeshPro>();
                TextMeshPro rotationMesh = rotationText.GetComponent<TextMeshPro>();

                if (positionMesh != null) positionMesh.text = ""; // Initialize empty text
                if (rotationMesh != null) rotationMesh.text = "";



                // Instantiate the additional object
                GameObject additionalObject = null;
                // Instantiate the additional prefab
                if (ObjCentrePrefab != null)
                {
                    additionalObject = Instantiate(ObjCentrePrefab, obj.transform.position, obj.transform.rotation);

                }

                // Store the text objects and the additional object in the dictionary
                textObjects[obj] = new GameObject[] { positionText, rotationText, additionalObject };

            }
        }
    }

    private void DisableInfoDisplay()
    {
        foreach (var texts in textObjects.Values)
        {
            foreach (GameObject textObj in texts)
            {
                if (textObj != null)
                {
                    Destroy(textObj); // Destroy position and rotation text objects, and additional object
                }
            }
        }

        textObjects.Clear(); // Clear the dictionary after destroying all objects
    }

    private void UpdateTextPositions()
    {
        foreach (var kvp in textObjects)
        {
            GameObject obj = kvp.Key; // Original object in the scene // kvp.Key retrieves the key of the current dictionary entry, which is the original GameObject in the scene (the object for which position and rotation are being displayed).
            GameObject[] texts = kvp.Value; // Associated text objects for position and rotation
                                            //The key-value pair structure we are using in the script is a C# feature, specifically implemented through the Dictionary<TKey, TValue> class in the .NET framework. 

            if (obj != null && texts.Length >= 2)
            {
                TextMeshPro positionMesh = texts[0]?.GetComponent<TextMeshPro>();
                TextMeshPro rotationMesh = texts[1]?.GetComponent<TextMeshPro>();

                if (positionMesh != null)
                {
                    Vector3 position = obj.transform.position;
                    positionMesh.text = $"Pos: X={position.x:F2} Y={position.y:F2} Z={position.z:F2}";
                    texts[0].transform.position = obj.transform.position + Vector3.up * PosTextoffset; // Update position above object
                }

                if (rotationMesh != null)
                {
                    Vector3 rotation = obj.transform.eulerAngles;
                    rotationMesh.text = $"Rot: X={rotation.x:F2} Y={rotation.y:F2} Z={rotation.z:F2}";
                    texts[1].transform.position = obj.transform.position + Vector3.up * RotTextoffset; // Update position below position text
                }

                // Optional: Make the text always face the camera
                for (int i = 0; i < 2; i++) // Only iterate over the first two elements (position and rotation text)
                {
                    if (texts[i] != null)
                    {
                        texts[i].transform.rotation = Quaternion.LookRotation(texts[i].transform.position - Camera.main.transform.position);
                    }
                }
                // This part make all the elements in texts group face the camera, I dont want the a ObjCentrePrefab or additionalObject(s) to change its orientation as its supposed to show me objects original orientation.
                //foreach (var textObj in texts)
                //{
                //    textObj.transform.rotation = Quaternion.LookRotation(textObj.transform.position - Camera.main.transform.position);
                //}
            }
        }
    }

    private List<GameObject> SceneObjects()
    {
        // Example: Gather all objects with a specific tag or layer
        // You can filter based on specific needs like tags, layers, or a list of known objects.

        List<GameObject> filteredObjects = new List<GameObject>();
        foreach (GameObject obj in GameObject.FindObjectsOfType<GameObject>())
        {
            // Check if the object has a tag in the targetTags list
            if (targetTags.Contains(obj.tag))
            {
                filteredObjects.Add(obj);
            }
        }

        return filteredObjects;
    }

    private void ValidateTags()
    {
        foreach (string tag in targetTags)
        {
            try
            {
                GameObject.FindWithTag(tag); // This throws an error if the tag doesn't exist
            }
            catch
            {
                Debug.LogWarning($"Tag '{tag}' does not exist. Please define it in Unity.");
            }
        }
    }

    //private void ValidateLayerNames()
    //{
    //    foreach (string layerName in targetLayers)
    //    {
    //        if (LayerMask.NameToLayer(layerName) == -1)
    //        {
    //            Debug.LogWarning($"Layer '{layerName}' does not exist. Please define it in Unity.");
    //        }
    //    }
    //}

}
