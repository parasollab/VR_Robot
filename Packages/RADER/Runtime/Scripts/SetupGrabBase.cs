using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class SetupGrabBase : MonoBehaviour
{
    // Start is called before the first frame update
    public GameObject Base;
    void Start()
    {
        GrabBaseSetup(Base);
    }

    void GrabBaseSetup(GameObject obj)
    {
        Rigidbody baseGrabRb = obj.GetComponent<Rigidbody>();
        baseGrabRb.isKinematic = true;
        baseGrabRb.useGravity = false;
        XRGrabInteractable grab = obj.AddComponent<XRGrabInteractable>();

        
        grab.colliders.Clear();

        MeshCollider meshCollider = obj.GetComponentInChildren<MeshCollider>();
        grab.colliders.Add(meshCollider);
        
        grab.selectExited.AddListener((SelectExitEventArgs interactor) => {
            GroundRobot(obj);
        });
    }

    void GroundRobot(GameObject grabJoint)
    {


        // Raycast downwards to find the ground
        RaycastHit hit;
        Vector3 rayOrigin = grabJoint.transform.position;
        Vector3 rayDirection = Vector3.down;
        float rayLength = 100f; // arbitrary length


        if (Physics.Raycast(rayOrigin, rayDirection, out hit, rayLength))
        {
            Vector3 endPosition = new Vector3(grabJoint.transform.position.x, hit.point.y, grabJoint.transform.position.z);

            grabJoint.transform.position = endPosition;
            grabJoint.transform.rotation = Quaternion.identity;

        }

    }
}
