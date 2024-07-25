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
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.State;
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Receiver.Rendering;
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Rendering;
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Theme.Primitives;

public class ProcessUrdf : MonoBehaviour
{
    public GameObject urdfModel;  // Reference to the base of the robot's URDF model

    public GameObject target;  // Reference to the target object for the CCDIK
    private List<KeyValuePair<GameObject, GameObject>> reparentingList = new List<KeyValuePair<GameObject, GameObject>>();

    private List<Tuple<float, float>> jointLimits = new List<Tuple<float, float>>();
    
    private List<bool> clampedMotionList = new List<bool>();

    private List<CCDIKJoint> ccdikJoints = new List<CCDIKJoint>();
    public ColorAffordanceThemeDatumProperty affordanceThemeDatum;

    // variables for sending messages to ROS
    public ROSConnection ros;
    private string topicName = "/joint_trajectory";
    protected List<Transform> knobs = new List<Transform>();

    private List<double> jointPositions = new List<double>();

    private List<String> jointNames = new List<String>();

    private bool recordROS = false;

    // string to retrieve UI prefab
    private String robotUIPath = "Assets/Prefabs/RobotOptions.prefab";



    public bool saveAsPrefab = false;

    void Awake()
    {
        if (urdfModel != null)
        {
            TraverseAndModify(urdfModel);
            reParent();
            createTarget(reparentingList[reparentingList.Count - 1].Key);
            urdfModel.AddComponent<SetupIK>();
            StartCoroutine(LoadUI()); 
            
            if(saveAsPrefab)
            {
                savePrefab(urdfModel.name);
            }
            if (ros == null) ros = ROSConnection.GetOrCreateInstance();
            ros.RegisterPublisher<JointTrajectoryMsg>(topicName);
            InvokeRepeating("sendJointPositionMessage", 1.0f, 1.0f);
        }
    }

    IEnumerator LoadUI() {
        AsyncOperationHandle<GameObject> asyncRobotUI = Addressables.LoadAssetAsync<GameObject>(robotUIPath);
        yield return asyncRobotUI;

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
            slider.value = knobs[dropdownIndex].GetComponentInParent<XRKnobAlt>().value;
            sliderText.text = knobs[dropdownIndex].transform.localRotation.eulerAngles.y.ToString();

            dropdown.onValueChanged.AddListener(delegate {
                dropdownIndex = dropdown.value;
                slider.value = knobs[dropdown.value].GetComponentInParent<XRKnobAlt>().value;
            });

            slider.onValueChanged.AddListener(delegate {
                knobs[dropdownIndex].GetComponentInParent<XRKnobAlt>().value = slider.value;
                sliderText.text = knobs[dropdownIndex].transform.localRotation.eulerAngles.y.ToString();
            });

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


            // // Add the XRKnobAlt
            XRKnobAlt knob = knobParent.AddComponent<XRKnobAlt>();
            knob.clampedMotion = clampedMotionList[i];
            knob.minAngle = jointLimits[i].Item1;
            knob.maxAngle = jointLimits[i].Item2;

            knob.handle = child.transform;
            
            createInteractionAffordance(child, knob, knobParent);
            Debug.Log("Teste");

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

    void createInteractionAffordance(GameObject child, XRKnobAlt knob, GameObject knobParent)
    {
        // create interaction affordance
            GameObject knobAffordance = new GameObject("KnobAffordance");
            knobAffordance.transform.parent = knobParent.transform;
            XRInteractableAffordanceStateProvider affordanceProvider = knobAffordance.AddComponent<XRInteractableAffordanceStateProvider>();
            affordanceProvider.interactableSource = knob;
            affordanceProvider.activateClickAnimationMode = XRInteractableAffordanceStateProvider.ActivateClickAnimationMode.Activated;



            GameObject colorAffordance = new GameObject("ColorAffordance");
            colorAffordance.transform.parent = knobAffordance.transform;

            // add xr interaction affordance receiver
            
            ColorMaterialPropertyAffordanceReceiver colorMaterialPropertyAffordanceReceiver = colorAffordance.AddComponent<ColorMaterialPropertyAffordanceReceiver>();
            colorMaterialPropertyAffordanceReceiver.replaceIdleStateValueWithInitialValue = true;
            MaterialPropertyBlockHelper materialPropertyBlockHelper = colorAffordance.GetComponent<MaterialPropertyBlockHelper>();
            colorMaterialPropertyAffordanceReceiver.affordanceThemeDatum = affordanceThemeDatum;
            MeshRenderer[] meshRenderers = child.GetComponentsInChildren<MeshRenderer>();
            materialPropertyBlockHelper.rendererTarget = meshRenderers[0];
            materialPropertyBlockHelper.enabled = true;
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

    // void setupIK()
    // {
    //     var lastPair = reparentingList[reparentingList.Count - 1];
    //     GameObject lastChild = lastPair.Key;


    //     // create target object for the last child
    //     GameObject instance = Instantiate(target, lastChild.transform.position, lastChild.transform.rotation);
    //     instance.transform.SetParent(lastChild.transform);
    //     // Optionally reset the local position, rotation, and scale
    //     instance.transform.localPosition = Vector3.zero;
    //     instance.transform.localRotation = Quaternion.identity;
    //     // ccdIK
    //     CCDIK ccdIK = lastChild.AddComponent<CCDIK>();
        
    //     ccdIK.joints = ccdikJoints.ToArray();
    //     ccdIK.Tooltip = lastChild.transform;
    //     ccdIK.Target = instance.transform;

    //     // xr events
    //     XRGrabInteractable grabInteractable = instance.GetComponent<XRGrabInteractable>();

    //     // On select enter set CCDIK activ
    //     grabInteractable.selectEntered.AddListener((SelectEnterEventArgs interactor) => {
    //         ccdIK.active = true;
    //     });

    //     grabInteractable.selectExited.AddListener((SelectExitEventArgs interactor) => {
    //         ccdIK.active = false;
    //     });
    // }

    void savePrefab(string name)
    {
        // Save the prefab
        string prefabPath = "Assets/Prefabs/"+name+".prefab";
        #if UNITY_EDITOR
        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(urdfModel, prefabPath, InteractionMode.AutomatedAction);
        #endif
    }

    void sendJointPositionMessage() {
        if (recordROS) {
            for (int i = 0; i < knobs.Count; i++) {
                jointPositions[i] = knobs[i].transform.localRotation.eulerAngles.y;
            }

            JointTrajectoryMsg jointTrajectory = new JointTrajectoryMsg();

            HeaderMsg header = new HeaderMsg
            {
                frame_id = urdfModel.name,
                stamp = new TimeMsg {
                    sec = (int)Time.time,
                    nanosec = (uint)((Time.time - (int)Time.time) * 1e9)
                }
            };
            jointTrajectory.header = header;
            jointTrajectory.joint_names = jointNames.ToArray();

            JointTrajectoryPointMsg jointTrajectoryPoint = new JointTrajectoryPointMsg
            {
                positions = jointPositions.ToArray(), 
                time_from_start = new DurationMsg(1, 0),
            };
            jointTrajectory.points = new JointTrajectoryPointMsg[] { jointTrajectoryPoint };
            ros.Publish(topicName, jointTrajectory);
        }
    }

}