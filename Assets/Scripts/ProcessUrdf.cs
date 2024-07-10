using System.Collections;
using System.Collections.Generic;
using Unity.VRTemplate;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class ProcessUrdf : MonoBehaviour
{
    public GameObject urdfModel;  // Reference to the base of the robot's URDF model

    void Start()
    {
        if (urdfModel != null)
        {
            TraverseAndModify(urdfModel);
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
        foreach (var script in scripts)
        {
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

            Vector3 originalRotation = obj.transform.eulerAngles;


            XRKnobAxes knob = obj.AddComponent<XRKnobAxes>();
            knob.rotationAxis.x = 1.0f;
            knob.originalRotation = originalRotation;

            // create child
            GameObject knobChild = new GameObject("KnobChild");
            knobChild.transform.position = obj.transform.position;
            knobChild.transform.rotation = obj.transform.rotation;
            knobChild.transform.localScale = obj.transform.localScale;

            // move all children to the child

            for (int i = obj.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = obj.transform.GetChild(i);
                Debug.Log(child.name);
                child.SetParent(knobChild.transform, false);
            }
            
            knobChild.transform.SetParent(obj.transform);

            knob.handle = knobChild.transform;

            MeshCollider meshCollider = obj.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                // If no MeshCollider found on the object, search its children
                meshCollider = obj.GetComponentInChildren<MeshCollider>();
            }
        

            knob.colliders.Clear();
            if (meshCollider != null)
            {
                knob.colliders.Add(meshCollider);
            }
            
            
            // knob.interactionManager.RegisterInteractable(knob);
        }
    }


}
