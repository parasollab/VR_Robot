
using System.Collections.Generic;
using Unity.Robotics.ROSTCPConnector;
using Unity.VRTemplate;
using UnityEngine;
using UnityEditor;
using System;
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.State;
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Receiver.Rendering;
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Rendering;
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Theme.Primitives;
using Unity.XR.CoreUtils;

public class ProcessUrdf : MonoBehaviour
{
    public GameObject urdfModel;  // Reference to the base of the robot's URDF model

    public GameObject target;  // Reference to the target object for the CCDIK
    public GameObject robotUI;  // Reference to the robot's UI prefab
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
    public bool saveAsPrefab = false;
    private int jointCount = 0;
    private GameObject grabJoint;
    void Awake()
    {
        if (urdfModel != null)
        {
            TraverseAndModify(urdfModel);
            reParent();
            SetupGrabBase setupBase = urdfModel.AddComponent<SetupGrabBase>();
            setupBase.Base = grabJoint;

            createTarget(reparentingList[reparentingList.Count - 1].Key);
            urdfModel.AddComponent<SetupIK>();
            

            urdfModel.AddComponent<SetupUI>();
            SetupUI ui = urdfModel.GetComponent<SetupUI>();
            ui.ros = ros; ui.topicName = topicName; ui.knobs = knobs; ui.jointPositions = jointPositions; ui.jointNames = jointNames; ui.robotUI = robotUI;

            #if UNITY_EDITOR
            savePrefab(urdfModel.name);
            #endif
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
            jointCount++;
            bool isClampedMotion = articulationBody.xDrive.upperLimit - articulationBody.xDrive.lowerLimit < 360;
            Tuple<float, float> jointLimit = new Tuple<float, float>(articulationBody.xDrive.lowerLimit, articulationBody.xDrive.upperLimit);

            if (articulationBody.xDrive.upperLimit - articulationBody.xDrive.lowerLimit == 0 && articulationBody.jointType == ArticulationJointType.RevoluteJoint) {
                isClampedMotion = false;
                jointLimit = new Tuple<float, float>(0, 360);
            }
            
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
            if(jointCount == 2)  grabJoint = obj;

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
        GameObject realLastChild = findRealLastChild(lastChild);
        // create target object for the last child
        GameObject target = Instantiate(this.target, realLastChild.transform.position, realLastChild.transform.rotation);
        target.name = "target";
        target.transform.SetParent(realLastChild.transform);
        target.transform.localPosition = Vector3.zero;
        target.transform.localRotation = Quaternion.identity;

    }

    GameObject findRealLastChild(GameObject lastChild) {
        foreach (Transform child in lastChild.transform) {
            if (child.gameObject.GetNamedChild("Collisions") != null && child.gameObject.GetNamedChild("Visuals") != null) {
                return findRealLastChild(child.gameObject);
            }
        }
        return lastChild;
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