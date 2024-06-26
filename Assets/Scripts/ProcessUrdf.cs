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
            Destroy(script); 
        }

        var articulationBody = obj.GetComponent<ArticulationBody>();
        if (articulationBody != null)
        {
            Destroy(articulationBody); 

            XRKnobTest knob = obj.AddComponent<XRKnobTest>();  

            knob.handle = obj.transform;

            // Assuming XRKnobTest has a public list or method to set colliders
            foreach (var meshCollider in obj.GetComponents<MeshCollider>())
            {
                knob.colliders.Add(meshCollider);  // You need to implement this method in XRKnobTest
            }
        }
    }

}
