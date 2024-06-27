using System.Collections;
using System.Collections.Generic;
using Unity.VRTemplate;
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

            XRKnobTest knob = obj.AddComponent<XRKnobTest>();
            knob.handle = obj.transform;

            MeshCollider meshCollider = obj.GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                // If no MeshCollider found on the object, search its children
                meshCollider = obj.GetComponentInChildren<MeshCollider>();
            }

    
            knob.colliders.Add(meshCollider);


            knob.interactionManager.RegisterInteractable(knob);
            // }
        }
    }

}
