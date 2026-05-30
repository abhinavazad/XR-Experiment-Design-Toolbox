using System;
using System.IO;
using UnityEngine;

public class PrefabPlacer : MonoBehaviour
{
    [Serializable]
    public class PrefabData
    {
        public int instanceID;
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

    [Header("Prefab Inputs")]
    [Tooltip("List of prefabs available for instantiation.")]
    public GameObject[] prefabs;

    [Header("JSON Input")]
    [Tooltip("Absolute file path for the JSON file.")]
    public string jsonFilePath;

    private AnchorsData anchorsData;

    [ContextMenu("Load and Place Prefabs")]
    public void LoadAndPlacePrefabs()
    {
        // Validate file path
        if (string.IsNullOrEmpty(jsonFilePath) || !File.Exists(jsonFilePath))
        {
            Debug.LogError($"JSON file not found at path: {jsonFilePath}");
            return;
        }

        // Load JSON
        try
        {
            string jsonContent = File.ReadAllText(jsonFilePath);
            anchorsData = JsonUtility.FromJson<AnchorsData>(jsonContent);

            if (anchorsData?.Anchors == null || anchorsData.Anchors.Length == 0)
            {
                Debug.LogError("JSON parsed but contains no anchors.");
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to read or parse JSON file: {ex.Message}");
            return;
        }

        Debug.Log($"Successfully loaded {anchorsData.Anchors.Length} anchors from JSON.");

        // Instantiate prefabs
        foreach (var anchor in anchorsData.Anchors)
        {
            GameObject prefab = FindPrefabByName(anchor.PrefabName);
            if (prefab == null)
            {
                Debug.LogError($"Prefab with name '{anchor.PrefabName}' not found in provided prefabs.");
                continue;
            }

            Vector3 position = new Vector3(anchor.Position.x, anchor.Position.y, anchor.Position.z);
            Quaternion rotation = new Quaternion(anchor.Rotation.x, anchor.Rotation.y, anchor.Rotation.z, anchor.Rotation.w);

            Instantiate(prefab, position, rotation);
            Debug.Log($"Instantiated prefab '{anchor.PrefabName}' at position {position} with rotation {rotation}.");
        }
    }

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
}
