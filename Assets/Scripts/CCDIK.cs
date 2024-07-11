using UnityEngine;
public class CCDIK : MonoBehaviour {
  public Transform Tooltip;
  public Transform Target;
  public CCDIKJoint[] joints;
  bool m_active = false;
  public bool active {
      get => m_active;
      set => m_active = value;
  }
  void Update() {
    if (m_active) {
      for (int j = 0; j < joints.Length; j++) {
        joints[j].Evaluate(Tooltip, Target, false);
      }
    }
  }
}