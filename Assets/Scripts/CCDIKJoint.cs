using UnityEngine;

public class CCDIKJoint : MonoBehaviour {
  public Vector3 axis = Vector3.right;
  public Vector3 originalRotation = Vector3.zero;
  public float maxAngle = 360;

  Vector3 perpendicular; 
  void Start() { 
    perpendicular = Perpendicular(axis); 
  }

  public static Vector3 Perpendicular(Vector3 vec) {
      return Mathf.Abs(vec.x) > Mathf.Abs(vec.z) ? new Vector3(-vec.y, vec.x, 0f)
                                                 : new Vector3(0f, -vec.z, vec.y);
  }

  public static Vector3 ConstrainToNormal(Vector3 direction, Vector3 normalDirection, float maxAngle) {
    if (maxAngle <= 0f) return normalDirection.normalized * direction.magnitude; if (maxAngle >= 180f) return direction;
    float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(direction.normalized, normalDirection.normalized), -1f, 1f)) * Mathf.Rad2Deg;
    return Vector3.Slerp(direction.normalized, normalDirection.normalized, (angle - maxAngle) / angle) * direction.magnitude;
  }


  public void Evaluate(Transform ToolTip, Transform Target, bool rotateToDirection = false) {
    //Rotate the assembly so the tooltip better matches the target position/direction
    transform.rotation = (rotateToDirection ? Quaternion.FromToRotation(ToolTip.up, Target.forward) : Quaternion.FromToRotation(ToolTip.position - transform.position, Target.position - transform.position)) * transform.rotation;

    //Enforce only rotating with the hinge
    transform.rotation = Quaternion.FromToRotation(transform.rotation * axis, transform.parent.rotation * axis) * transform.rotation;
    
    //Enforce Joint Limits
    // transform.rotation = Quaternion.FromToRotation(transform.rotation * perpendicular, ConstrainToNormal(transform.rotation * perpendicular, transform.parent.rotation * perpendicular, maxAngle)) * transform.rotation;

    // Align the rotation with the original orientation of the joint
    transform.rotation = transform.rotation * Quaternion.Euler(originalRotation);

  }
  
}