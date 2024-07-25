using System.Collections;
using System.Collections.Generic;
using Unity.Robotics.UrdfImporter;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;
using RosMessageTypes.Trajectory;
using RosMessageTypes.BuiltinInterfaces;
using Unity.VRTemplate;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEditor;
using Unity.VisualScripting;
using UnityEngine.AddressableAssets;
using System;
using UnityEngine.ResourceManagement.AsyncOperations;
using Unity.XR.CoreUtils;
using UnityEngine.UI;
using TMPro;
using System.Threading.Tasks;

public class ProcessUrdf : MonoBehaviour
{
    public GameObject urdfModel;  // Reference to the base of the robot's URDF model

    public GameObject target;  // Reference to the target object for the CCDIK
    private List<KeyValuePair<GameObject, GameObject>> reparentingList = new List<KeyValuePair<GameObject, GameObject>>();

    private List<Tuple<float, float>> jointLimits = new List<Tuple<float, float>>();
    
    private List<bool> clampedMotionList = new List<bool>();

    private List<CCDIKJoint> ccdikJoints = new List<CCDIKJoint>();

    // variables for sending messages to ROS
    public ROSConnection ros;
    private string topicName = "/joint_trajectory";
    protected List<Transform> knobs = new List<Transform>();

    private List<double> jointPositions = new List<double>();

    private List<String> jointNames = new List<String>();

    private bool recordROS = false;

    GameObject robotUI;

    private String robotUIPath = "Assets/Prefabs/RobotOptions.prefab";

    void Awake()
    {
        if (urdfModel != null)
        {
            TraverseAndModify(urdfModel);
            reParent();
            
            StartCoroutine(InitializeAsync());
        }
    }

    IEnumerator InitializeAsync() {
        Task uiTask = LoadRobotUIAsync();
        yield return new WaitUntil(() => uiTask.IsCompleted);

        if (uiTask.IsCompletedSuccessfully)
        {
            createTarget(reparentingList[reparentingList.Count - 1].Key);
            urdfModel.AddComponent<SetupIK>(); 
            
            #if UNITY_EDITOR
            savePrefab(urdfModel.name);
            #endif
        }
        else
        {
            Debug.LogError("Failed to load the Robot UI.");
        }
    }

    void TraverseAndModify(GameObject obj)
    {
        if (obj == null) return;

        // Process the current object
        RemoveAndModifyComponents(obj);
        
        // Recursively process each child
        foreach (Transform child in obj.transform)
        {
            TraverseAndModify(child.gameObject);
        }
    }

    void RemoveAndModifyComponents(GameObject obj)
    {
        var scripts = new List<MonoBehaviour>(obj.GetComponents<MonoBehaviour>());
        bool fixedJoint = false;
        foreach (var script in scripts)
        {   
            fixedJoint = script.GetType().Name == "UrdfJointFixed";
            DestroyImmediate(script); 
        }

        var articulationBody = obj.GetComponent<ArticulationBody>();
        if (articulationBody != null)
        {

            bool isClampedMotion = articulationBody.xDrive.upperLimit - articulationBody.xDrive.lowerLimit < 360;
            Tuple<float, float> jointLimit = new Tuple<float, float>(articulationBody.xDrive.lowerLimit, articulationBody.xDrive.upperLimit);
            
            DestroyImmediate(articulationBody);

            // add rigidbody
            var rb = obj.AddComponent<Rigidbody>();
            rb.mass = 1.0f;
            rb.useGravity = false;
            rb.isKinematic = true;
            // if fixedJoint we dont add XRGrabInteractable
            if(!fixedJoint)
            { 


                GameObject originalParent = obj.transform.parent.gameObject;
                GameObject knobParent = new GameObject("KnobParent_" + obj.name);

                knobParent.transform.parent = originalParent.transform;

                // Store the object and its new parent for later re-parenting
                reparentingList.Add(new KeyValuePair<GameObject, GameObject>(obj, knobParent));
                clampedMotionList.Add(isClampedMotion);
                jointLimits.Add(jointLimit);
            }

        }
    }

    void reParent()
    {

        for (int i = reparentingList.Count - 1; i >= 0; i--)
        {
            var pair = reparentingList[i];
            GameObject child = pair.Key;
            GameObject knobParent = pair.Value;
            jointNames.Add(child.name);

            knobParent.transform.position = child.transform.position;
            knobParent.transform.rotation = child.transform.rotation;

            // // Set the new parent
            child.transform.parent = knobParent.transform;

            // zero out child's local position and rotation
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;

            // // Add CCDIK components to the child, and add references to the list
            CCDIKJoint ccdik = child.AddComponent<CCDIKJoint>();
            ccdik.axis = new Vector3(0, 1, 0);
            ccdikJoints.Add(ccdik);


            // // Add the XRKnob
            XRKnob knob = knobParent.AddComponent<XRKnob>();
            knob.clampedMotion = clampedMotionList[i];
            knob.minAngle = jointLimits[i].Item1;
            knob.maxAngle = jointLimits[i].Item2;

            knob.handle = child.transform;

            // Use .Prepend to reverse the joint order
            knobs.Add(child.transform);
            jointPositions.Add(child.transform.localRotation.eulerAngles.y);

            // // Check for MeshCollider on the child or its descendants
            MeshCollider meshCollider = child.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = child.GetComponentInChildren<MeshCollider>();
            }

            // Clear existing colliders and add the found one if any
            knob.colliders.Clear();
            if (meshCollider != null)
            {
                knob.colliders.Add(meshCollider);
            }
        }
    }

    void createTarget(GameObject lastChild)
    {
        // create target object for the last child
        GameObject target = Instantiate(this.target, lastChild.transform.position, lastChild.transform.rotation);
        target.name = "target";
        target.transform.SetParent(lastChild.transform);
        target.transform.localPosition = Vector3.zero;
        target.transform.localRotation = Quaternion.identity;

    }

    async Task LoadRobotUIAsync() {
        AsyncOperationHandle<GameObject> asyncRobotUI = Addressables.LoadAssetAsync<GameObject>(robotUIPath);
        await asyncRobotUI.Task;

        if (asyncRobotUI.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject robotUI = asyncRobotUI.Result;
            robotUI = Instantiate(robotUI, urdfModel.transform);
            GameObject contentGameObject = robotUI.GetNamedChild("Spatial Panel Scroll").GetNamedChild("Scroll View").GetNamedChild("Viewport").GetNamedChild("Content");

            // button
            GameObject buttonObject = contentGameObject.GetNamedChild("List Item Button").GetNamedChild("Text Poke Button");
            Button button = buttonObject.GetComponent<Button>();
            TextMeshProUGUI buttonText = buttonObject.GetNamedChild("Button Front").GetNamedChild("Text (TMP) ").GetComponent<TextMeshProUGUI>();

            button.onClick.AddListener(() => {
                if (recordROS == true) {
                    recordROS = false;
                    buttonText.text = "Start Recording";
                } else {
                    recordROS = true;
                    buttonText.text = "Stop Recording";
                }
            });

            // dropdown and slider
            TMP_Dropdown dropdown = contentGameObject.GetNamedChild("List Item Dropdown").GetNamedChild("Dropdown").GetComponent<TMP_Dropdown>();
            Slider slider = contentGameObject.GetNamedChild("List Item Slider").GetNamedChild("MinMax Slider").GetComponent<Slider>();
            TextMeshProUGUI sliderText = slider.gameObject.GetNamedChild("Value Text").GetComponent<TextMeshProUGUI>();

            dropdown.AddOptions(jointNames);
            int dropdownIndex = 0;
            slider.value = knobs[dropdownIndex].GetComponentInParent<XRKnob>().value;
            sliderText.text = knobs[dropdownIndex].transform.localRotation.eulerAngles.y.ToString();

            dropdown.onValueChanged.AddListener(delegate {
                dropdownIndex = dropdown.value;
                slider.value = knobs[dropdown.value].GetComponentInParent<XRKnob>().value;
            });

            slider.onValueChanged.AddListener(delegate {
                knobs[dropdownIndex].GetComponentInParent<XRKnob>().value = slider.value;
                sliderText.text = knobs[dropdownIndex].transform.localRotation.eulerAngles.y.ToString();
            });

            robotUI.transform.SetParent(urdfModel.transform);

        } 
    }


    void savePrefab(string name)
    {
        // Save the prefab
        string prefabPath = "Assets/Prefabs/"+name+".prefab";
        #if UNITY_EDITOR
        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(urdfModel, prefabPath, InteractionMode.AutomatedAction);
        #endif
    }
}