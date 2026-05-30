using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization; // For InvariantCulture
using Unity.VisualScripting;
using UnityEngine;

public class ReplayMovement : MonoBehaviour
{
    //private SceneReloader scenereloader;//Not needed but just kept to know that we have referenced another script in this script

    public GameObject avatarGameObject; // Assign the avatar GameObject in the Inspector
    public TextAsset csvFile; // Assign the CSV file in the Inspector

    private class AvatarData
    {
        public float elapsedTime;
        public Vector3 position;
        public Quaternion rotation;
    }

    private List<AvatarData> movementData = new List<AvatarData>();
    private int currentIndex = 0;

    [Header("Realignment")]
    [Tooltip("Decide weather to realign 1st Anchor as orign")]
    private Vector3 anchor0Pos;    // Original position of anchor0
    private Quaternion anchor0Rot; // Original rotation of anchor0

    void Start()
    {
        var scenereloader = FindObjectOfType<SceneReloader>();
        if (scenereloader == null)
        {
            Debug.Log("No SceneReloader found in scene!");
            anchor0Pos = Vector3.zero; // Initializes to (0, 0, 0)
            anchor0Rot = Quaternion.identity; // Initializes to no rotation (0, 0, 0, 1)
        }
        else
        {
            anchor0Pos = scenereloader.anchor0Pos;
            anchor0Rot = scenereloader.anchor0Rot;

        }

            // Parse the CSV file
            string[] lines = csvFile.text.Split('\n');
        for (int i = 1; i < lines.Length; i++) // Skip header
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] columns = lines[i].Split(',');

            if (columns.Length < 9) continue; // Skip incomplete rows

            // Parse values using InvariantCulture to ensure decimal points are interpreted correctly
            if (float.TryParse(columns[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float elapsedTime) &&
                float.TryParse(columns[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float posX) &&
                float.TryParse(columns[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float posY) &&
                float.TryParse(columns[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float posZ) &&
                float.TryParse(columns[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float rotX) &&
                float.TryParse(columns[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float rotY) &&
                float.TryParse(columns[7], NumberStyles.Float, CultureInfo.InvariantCulture, out float rotZ) &&
                float.TryParse(columns[8], NumberStyles.Float, CultureInfo.InvariantCulture, out float rotW))
            {
                movementData.Add(new AvatarData
                {
                    elapsedTime = elapsedTime,
                    position = new Vector3(posX, posY, posZ),
                    rotation = new Quaternion(rotX, rotY, rotZ, rotW)
                });

                Debug.Log($"Parsed position: {new Vector3(posX, posY, posZ)}, rotation: {new Quaternion(rotX, rotY, rotZ, rotW)}, time elapsed: {elapsedTime} ms");
            }
            else
            {
                Debug.LogError($"Failed to parse line: {lines[i]}");
            }
        }

        Debug.Log($"Loaded {movementData.Count} data points.");
        StartCoroutine(MoveAvatar());
    }

    private IEnumerator MoveAvatar()
    {
        while (currentIndex < movementData.Count - 1)
        {
            AvatarData currentData = movementData[currentIndex];
            AvatarData nextData = movementData[currentIndex + 1];

            Vector3 oldPos = new Vector3(currentData.position.x, currentData.position.y, currentData.position.z);
            Quaternion oldRot = new Quaternion(currentData.rotation.x, currentData.rotation.y, currentData.rotation.z, currentData.rotation.w);

            // Relative Transformations to realign 1st anchors to origin
            // Shift anchor0 to (0,0,0) with identity rotation
            Vector3 shiftedPos = oldPos - anchor0Pos;                      // remove anchor0's old position
            shiftedPos = Quaternion.Inverse(anchor0Rot) * shiftedPos;      // rotate by the inverse of anchor0's old rotation

            Quaternion shiftedRot = Quaternion.Inverse(anchor0Rot) * oldRot;

            avatarGameObject.transform.position = shiftedPos;
            avatarGameObject.transform.rotation = shiftedRot;


            float RelativeTime = (currentData.elapsedTime - movementData[0].elapsedTime) / 1000f; // Convert ms to seconds
            float waitTime = (nextData.elapsedTime - currentData.elapsedTime) / 1000f; // Convert ms to seconds

            Debug.Log($"Moved to position: {currentData.position}, rotation: {currentData.rotation}, time elapsed: {RelativeTime} ms");

            yield return new WaitForSeconds(waitTime);
            currentIndex++;
        }
    }
}
