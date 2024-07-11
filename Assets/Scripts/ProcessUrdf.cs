using System.Collections;
using System.Collections.Generic;
using Unity.Robotics.UrdfImporter;
using Unity.VRTemplate;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class ProcessUrdf : MonoBehaviour
{
    public GameObject urdfModel;  // Reference to the base of the robot's URDF model
    private List<KeyValuePair<GameObject, GameObject>> reparentingList = new List<KeyValuePair<GameObject, GameObject>>();

    void Start()
    {
        if (urdfModel != null)
        {
            TraverseAndModify(urdfModel);
            reParent();
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
            }

        }
    }

    void reParent()
    {
        // Iterate from the end of the list to the beginning
        for (int i = reparentingList.Count - 1; i >= 0; i--)
        {
            var pair = reparentingList[i];
            GameObject child = pair.Key;
            GameObject knobParent = pair.Value;

            knobParent.transform.position = child.transform.position;
            knobParent.transform.rotation = child.transform.rotation;

            // Quaternion originalRotation = child.transform.rotation;

            // // Set the new parent
            child.transform.parent = knobParent.transform;

            // zero out child's local position and rotation
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;

            // // Add the XRKnob
            XRKnob knob = knobParent.AddComponent<XRKnob>();

            knob.handle = child.transform;

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


}



  // GameObject knobParent = new GameObject("KnobParent_"+obj.name);

            // XRKnobAxes knob = knobParent.AddComponent<XRKnobAxes>();
            // knob.rotationAxis.x = 1.0f;
            // knob.originalRotation = originalRotation;
           
            // knobParent.transform.position = obj.transform.position;
            // knobParent.transform.rotation = obj.transform.rotation;
            // knobParent.transform.localScale = obj.transform.localScale;
            
            // obj.transform.SetParent(knobParent.transform);
            // knobParent.transform.SetParent(originalParent.transform);

            // knob.handle = obj.transform;

            // MeshCollider meshCollider = obj.GetComponent<MeshCollider>();
            // if (meshCollider == null)
            // {
            //     // If no MeshCollider found on the object, search its children
            //     meshCollider = obj.GetComponentInChildren<MeshCollider>();
            // }
        

            // knob.colliders.Clear();
            // if (meshCollider != null)
            // {
            //     knob.colliders.Add(meshCollider);
            // }