# Project Setup Instructions

Clone the project and open it in Unity. All necessary packages should already be installed.

## Robot Setup Instructions

To import a new URDF robot into the scene, ensure the following folders are present:

- `Materials`
- `ur_description`
- `robot.urdf`

The `Materials`, `ur_description`, and `robot.urdf` files must be in the same folder.

By clicking on the `robot.urdf` file, then hovering over the **Assets** label on the top bar, and selecting **Import Robot from URDF**, a new URDF robot can be imported into the scene.

<img src="image/readme/1722649565718.png" alt="Import Robot from URDF" width="200" height="400">

To allow the imported robot to be controlled in VR, the `Process URDF` script must be added to the scene, and its fields must be populated.

<img src="image/readme/1722649789693.png" alt="Process URDF script" width="400" height="150">

- Drag the URDF-imported robot from the scene into the `URDF Model` field.
- Drag the `Target Prefab` and the `Robot Options Prefab` from the `Assets/Prefabs` folder into the `URDF Parser` field as shown in the image above.
- Drag the `Affordance Theme` from the `Prefabs` folder into the appropriate field.
- Set the **Save as Prefab** checkbox to `true` to allow the robot to be saved as a Prefab in the `Prefabs` folder.

### Components Explanation

- **Target Prefab:** The white sphere seen on the robot's end effector, allowing for inverse kinematics control.
- **Robot Options Prefab:** The user menu enabling ROS joint recordings and control of specific joint angles via sliders.
- **Affordance Theme:** Highlights the joints being manipulated by the user and can be found in the `Prefabs` folder.
