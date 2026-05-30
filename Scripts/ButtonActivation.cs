using System;
using System.Collections.Generic;
using UnityEngine;
//using UnityEngine.UI;
using TMPro;
using UnityEngine.XR;
using System.IO;
using static UnityEngine.InputSystem.InputSettings;
using UnityEngine.UI;

#if OCULUS
using UnityEngine.XR.OpenXR;
#endif

//### How It Works:
//1. * *Button to Toggle One Object at a Time:**
//   -Each button in `buttons` corresponds to a specific game object in `OneAtaTime`. Clicking a button enables its associated object and disables the rest.

//2. **On-Off List Toggle:**
//   -Pressing `OVRInput.Button.Three` toggles the active state of all objects in the `onOffList` list.

//### Setup Instructions:
//1. **Attach the Script:**
//   -Add the script to an empty GameObject in your scene.

//2. **Assign GameObjects and Buttons:**
//   -Populate the `OneAtaTime` and `buttons` lists in the Inspector.Ensure their order matches (e.g., Button 1 -> Object 1).

//3. * *Assign On - Off List: **
//   -Populate the `onOffList` with the GameObjects you want to toggle on/off in bulk.



public class ButtonActivation : MonoBehaviour
{
    [Header("XR Mode selector")]
    public TMP_Dropdown xrModeDropdown; // Assign this in the Inspector
    public static string XRMode { get; private set; } = "PR"; // Default to PR (Physical Reality)
    private List<string> xrModes = new List<string> { "PR", "AR", "VR", "MR" };

    public TMP_InputField AnchorFilename_input; // Assign in Inspector (UI element to update)



    [Header("On-Off Toggle List")]
    [SerializeField] private List<GameObject> onOffList;  // List of GameObjects to toggle on/off

    [Header("Global debug logger setup")]
    public TextMeshProUGUI PureDebugText; // Reference to the UI TextMeshPro element
    public string logMessages = ""; // To store all log messages
    private string displaylogMessages = ""; // To store all log messages

    private int maxMessages = 50;    // Limit the number of messages displayed

#if MAGICLEAP
    // Magic Leap inputs to detect if the user is pressing Menu, Bumper, or Trigger.
    private MagicLeapInputs _magicLeapInputs;
    private MagicLeapInputs.ControllerActions _controllerActions;

    void OnEnable()
    {

        // Initialize Input
        if (_magicLeapInputs == null)
        {
            _magicLeapInputs = new MagicLeapInputs();
            _controllerActions = new MagicLeapInputs.ControllerActions(_magicLeapInputs);
        }

        // Enable input if it was disabled previously
        _magicLeapInputs.Enable();

        _controllerActions.Menu.started += ctx => ToggleOnOffList(); ; // Menu button will toggle UI

        // Subscribe to Unity's log message callback
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable()
    {
        // Unsubscribe from controller input when the object is disabled
        if (_magicLeapInputs != null)
        {
            _controllerActions.Menu.started -= ctx => ToggleOnOffList(); ; // Menu button will toggle UI
            _magicLeapInputs.Disable();

            // Unsubscribe when the object is disabled
            Application.logMessageReceived -= HandleLog;
        }
    }
#endif

#if OCULUS
    void OnEnable()
    {
        // Subscribe to Unity's log message callback
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        // Unsubscribe when the object is disabled
        Application.logMessageReceived -= HandleLog;
    }
#endif

    // Function to handle Unity's log messages and print it on UI:
    // This code will log all Unity log messages (not just those from the current script)
    // because it subscribes to Unity's global Application.logMessageReceived event.
    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Append the log message
        logMessages += logString + "\n";
        displaylogMessages += logString + "\n";

        // Limit the number of lines displayed
        string[] lines = displaylogMessages.Split('\n');
        if (lines.Length > maxMessages)
        {
            displaylogMessages = string.Join("\n", lines, lines.Length - maxMessages, maxMessages);
        }

        // Update the UI TextMeshPro element
        if (PureDebugText != null)
        {
            PureDebugText.text = "All:" + displaylogMessages;
        }
    }
    public void saveFileatStop()
    {
        // Set log file path (stores in Application.persistentDataPath)
        string logFilePath = Path.Combine(ExperimentManager.filePath, $"Full_DebugLogs_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

        // Optional: Clear the file at the start
        File.WriteAllText(logFilePath, logMessages);
    }

    private void Start()
    {
        DetectPlatform();
        KeepHeadsetAwake(); // Check if this function works or just physically tape the sensor

#if OCULUS
        XRMode = "MR";

#endif
#if MAGICLEAP
        XRMode = "AR";
#endif
#if UNITY_EDITOR
        XRMode = "VR";
#endif

        PopulateXRmodeDropdown();
        xrModeDropdown.onValueChanged.AddListener(UpdateXRMode);
        UpdateXRMode(xrModeDropdown.value); // Ensure correct mode is set at start
    }

    private void Update()
    {
#if OCULUS
        // Check for input to toggle the on-off list
        if (OVRInput.GetDown(OVRInput.Button.Three)) // Button X to toggle on-off list
        {
            ToggleOnOffList();
        }
#endif
    }

    private void PopulateXRmodeDropdown()
    {
        xrModeDropdown.ClearOptions(); // Remove any existing options
        xrModeDropdown.AddOptions(xrModes); // Add the predefined XR modes
        xrModeDropdown.value = xrModes.IndexOf(XRMode); // Set default selection
        xrModeDropdown.RefreshShownValue(); // Refresh the UI display
    }

    private void UpdateXRMode(int index)
    {
        if (index >= 0 && index < xrModes.Count)
        {
            XRMode = xrModes[index];
            Debug.Log($"XR Mode switched to: {XRMode}");
        }

        // Update the input field if PR is selected
        if (XRMode == "PR" && AnchorFilename_input != null)
        {
            AnchorFilename_input.text = "Anchor_data11PR";
        }
        else
        {
            AnchorFilename_input.text = "Anchor_data11";

        }
    }

    private void ToggleOnOffList()
    {
        if (onOffList.Count == 0) return; // Ensure the list is not empty

        // Determine the new state based on the first object in the list
        bool newState = !onOffList[0].activeSelf;

        // Apply the new state to all objects in the list
        foreach (var go in onOffList)
        {
            if (go != null) go.SetActive(newState);
        }

        Debug.Log($"On-Off list toggled to: {newState}");
    }

    private void KeepHeadsetAwake()
    {
#if OCULUS
        Application.runInBackground = true; // Keeps the app running in the background.
        //OVRManager.runInBackground = true; // Keeps the app running in the background.
#elif MAGICLEAP
        Application.runInBackground = true; // Keeps the app running in the background.
#endif
    }

    void OnDestroy()
    {

    }

    void DetectPlatform()
    {
        // Try XRSettings first
        Debug.Log("Is XRSettings.isDeviceActive? " + XRSettings.isDeviceActive);

        if (XRSettings.isDeviceActive)
        {
            string deviceName = XRSettings.loadedDeviceName.ToLower();
            Debug.Log("Active XR device: " + XRSettings.loadedDeviceName);

            if (deviceName.Contains("oculus"))
            {
                Debug.Log("Oculus headset detected.");
            }
            else if (deviceName.Contains("vive"))
            {
                Debug.Log("HTC Vive headset detected.");
            }
            else if (deviceName.Contains("magicleap"))
            {
                Debug.Log("Magic Leap detected.");
            }
            else
            {
                Debug.Log("Unknown XR device: " + deviceName);
            }
            return;
        }

#if OCULUS
        // Check OpenXR runtime - just for checking platform
        string runtimeName = OpenXRRuntime.name;
        Debug.Log("OpenXR Runtime: " + runtimeName);

        if (!string.IsNullOrEmpty(runtimeName))
        {
            //Debug.Log("OpenXR Runtime: " + runtimeName);

            if (runtimeName.ToLower().Contains("oculus"))
            {
                Debug.Log("Oculus device detected via OpenXR.");
            }
            else if (runtimeName.ToLower().Contains("steamvr"))
            {
                Debug.Log("SteamVR/HTC Vive detected via OpenXR.");
            }
            else
            {
                Debug.Log("Unknown OpenXR runtime.");
            }
        }
#endif

        // Retrieve all devices with the HeadMounted characteristic
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, devices);

        // Iterate through the devices to detect their names
        foreach (var device in devices)
        {
            Debug.Log("Device found: " + device.name);

            if (device.name.ToLower().Contains("oculus"))
            {
                Debug.Log("Oculus headset detected.");
            }
            else if (device.name.ToLower().Contains("vive"))
            {
                Debug.Log("HTC Vive detected.");
            }
            else if (device.name.ToLower().Contains("index"))
            {
                Debug.Log("Valve Index detected.");
            }
            else
            {
                Debug.Log("Unknown headset: " + device.name);
            }
        }

        if (devices.Count == 0)
        {
            Debug.Log("No head-mounted devices detected.");
        }

        // Final fallback to SystemInfo
        string deviceModel = SystemInfo.deviceModel.ToLower();
        Debug.Log("Device Model: " + deviceModel);

        if (deviceModel.Contains("quest"))
        {
            Debug.Log("Oculus Quest detected.");
        }
        else if (deviceModel.Contains("vive"))
        {
            Debug.Log("HTC Vive detected.");
        }
        else
        {
            Debug.Log("Unknown device.");
        }

        PrintDeviceDetails();

#if OCULUS
        Debug.Log("## OCULUS - Oculus platform detected.");
#elif OPENVR
        Debug.Log("##  OPENVR - HTC Vive (OpenVR) platform detected.");
#elif MAGICLEAP
        Debug.Log(" ## MAGICLEAP - Magic Leap platform detected.");
#elif UNITY_EDITOR
        Debug.Log(" ## UNITY_EDITOR - Unity editor is detected.");
#else
        Debug.Log("## Unknown or unsupported platform.");
#endif
    }

    void PrintDeviceDetails()
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevices(devices);

        foreach (var device in devices)
        {
            Debug.Log($"Device Name: {device.name}");
            Debug.Log($"Manufacturer: {device.manufacturer}");
            Debug.Log($"Characteristics: {device.characteristics}");
        }
    }



    //private void ToggleOneAtaTime(int index, List<GameObject> OneAtaTime)
    //{
    //    if (OneAtaTime == null || OneAtaTime.Count == 0)
    //    {
    //        Debug.LogWarning("OneAtaTime list is null or empty.");
    //        return;
    //    }

    //    // Disable all game objects in the OneAtaTime list
    //    foreach (var obj in OneAtaTime)
    //    {
    //        if (obj != null) obj.SetActive(false);
    //    }

    //    // Enable the selected game object
    //    if (index >= 0 && index < OneAtaTime.Count)
    //    {
    //        if (OneAtaTime[index] != null) OneAtaTime[index].SetActive(true);
    //    }
    //    else
    //    {
    //        Debug.LogWarning($"Index {index} is out of range for the OneAtaTime list.");
    //    }
    //}

}
