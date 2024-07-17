using System.Collections;
using System.Collections.Generic;
using Unity.Robotics.UrdfImporter;
using Unity.VRTemplate;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEditor;
using Unity.VisualScripting;


public class SetupIK : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject target;  // Reference to the target object for the CCDIK
    private List<CCDIKJoint> ccdikJoints = new List<CCDIKJoint>();
    private GameObject lastChild;
    void Start()
    {
        TraverseAndAnalyze(this.gameObject);
        if(lastChild == null)
        {
            Debug.LogError("No XRKnob found in children, error in prefabSetup");
            return;
        }
        if(ccdikJoints.Count == 0)
        {
            Debug.LogError("No CCDIKJoint found in children, error in prefabSetup");
            return;
        }
        if(target == null)
        {
            Debug.LogError("No target object found, assign target object in script");
            return;
        }
        setupIK(lastChild);
    }


    void TraverseAndAnalyze(GameObject obj)
    {
    
        if (obj == null) return;
        if(obj.GetComponent<XRKnob>() != null)
        {
            lastChild = obj.gameObject;
        }
        if(obj.GetComponent<CCDIKJoint>() != null)
        {
            ccdikJoints.Add(obj.GetComponent<CCDIKJoint>());
        }
        
        // Recursively process each child
        foreach (Transform child in obj.transform)
        {
            TraverseAndAnalyze(child.gameObject);
        }
    }

    void setupIK(GameObject lastChild)
    {
        // create target object for the last child
        GameObject instance = Instantiate(target, lastChild.transform.position, lastChild.transform.rotation);
        instance.transform.SetParent(lastChild.transform);
        // Optionally reset the local position, rotation, and scale
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        // ccdIK
        CCDIK ccdIK = lastChild.AddComponent<CCDIK>();
        
        ccdIK.joints = ccdikJoints.ToArray();
        ccdIK.Tooltip = lastChild.transform;
        ccdIK.Target = instance.transform;

        // xr events
        XRGrabInteractable grabInteractable = instance.GetComponent<XRGrabInteractable>();

        // On select enter set CCDIK activ
        grabInteractable.selectEntered.AddListener((SelectEnterEventArgs interactor) => {
            ccdIK.active = true;
        });

        grabInteractable.selectExited.AddListener((SelectExitEventArgs interactor) => {
            ccdIK.active = false;
        });
    }
}
