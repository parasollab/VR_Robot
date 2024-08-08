using System;
using System.Collections.Generic;
using TMPro;
using Unity.VRTemplate;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.UI;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Trajectory;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Std;
using RosMessageTypes.Sensor;
using RosMessageTypes.Hri;
using UnityEngine.XR;

public class SetupUI : MonoBehaviour
{
    public ROSConnection ros;
    public GameObject queryUI;
    public GameObject robotUI;
    public GameObject mirrorUI;
    public String trajTopicName = "/joint_trajectory";
    public String queryTopicName = "/joint_query";
    public String inputStateTopicName = "/physical_joint_state";
    public String outputStateTopicName = "/virtual_joint_state";
    public List<Transform> knobs;
    public List<double> jointPositions;
    public List<String> jointNames;
    public float recordInterval = 0.5f;
    public float publishStateInterval = 0.2f;

    private bool recordROS = false;
    private List<double> startPositions;
    private List<double> goalPositions;
    private List<JointTrajectoryPointMsg> jointTrajectoryPoints = new List<JointTrajectoryPointMsg>();
    private bool mirrorInputState = false;
    private bool publishState = true;

    private GameObject startButtonObject;
    private GameObject goalButtonObject;
    private GameObject queryButtonObject;

    private GameObject recordButtonObject;
    private int recordStartTime;

    private GameObject mirrorButtonObject;
    private GameObject publishStateButtonObject;

    private List<double> jointTorques;
    private Dictionary<Transform, float> previousAngles = new Dictionary<Transform, float>();
    private Dictionary<Transform, float> momentsOfInertia = new Dictionary<Transform, float>();

    void Start()
    {
        Debug.Log("SetupUI Start");
        if (ros == null) ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<JointQueryMsg>(queryTopicName);
        ros.RegisterPublisher<JointTrajectoryMsg>(trajTopicName);
        ros.RegisterPublisher<JointStateMsg>(outputStateTopicName);
        ros.Subscribe<JointStateMsg>(inputStateTopicName, MirrorStateCallback);

        LoadQueryInterface();
        LoadUI();
        LoadMirrorInterface();
        
        InitializeKnobData();
    }

    void InitializeKnobData()
    {
        foreach (var knob in knobs)
        {
            previousAngles[knob] = knob.transform.localEulerAngles.y;
            momentsOfInertia[knob] = 0.5f; // Example value, adjust as necessary
        }
    }

    void LoadMirrorInterface()
    {
        Debug.Log("LoadMirrorInterface");
        if (mirrorUI == null)
        {
            Debug.LogError("error loading UI");
        }
        mirrorUI = Instantiate(mirrorUI, transform);
        GameObject contentGameObject = mirrorUI.GetNamedChild("Spatial Panel Scroll").GetNamedChild("Scroll View").GetNamedChild("Viewport").GetNamedChild("Content");

        // Mirror input button
        mirrorButtonObject = contentGameObject.GetNamedChild("Mirror Input Button").GetNamedChild("Text Poke Button");
        Button mirrorButton = mirrorButtonObject.GetComponent<Button>();
        TextMeshProUGUI mirrorButtonText = mirrorButtonObject.GetNamedChild("Button Front").GetNamedChild("Text (TMP) ").GetComponent<TextMeshProUGUI>();

        mirrorButton.onClick.AddListener(() =>
        {
            Debug.Log("mirrorButton.onClick");
            if (mirrorInputState)
            {
                stopMirroring();
            }
            else
            {
                startMirroring();
            }
        });

        // Publish state button
        publishStateButtonObject = contentGameObject.GetNamedChild("Publish State Button").GetNamedChild("Text Poke Button");
        Button publishStateButton = publishStateButtonObject.GetComponent<Button>();
        TextMeshProUGUI publishStateButtonText = publishStateButtonObject.GetNamedChild("Button Front").GetNamedChild("Text (TMP) ").GetComponent<TextMeshProUGUI>();

        publishStateButton.onClick.AddListener(() =>
        {
            Debug.Log("publishStateButton.onClick");
            if (publishState)
            {
                publishState = false;
                publishStateButtonText.text = "Start Publishing";
            }
            else
            {
                publishState = true;
                publishStateButtonText.text = "Stop Publishing";
            }
        });

        InvokeRepeating("PublishState", 1.0f, publishStateInterval);
    }

    public void startMirroring()
    {
        mirrorInputState = true;
        TextMeshProUGUI mirrorButtonText = mirrorButtonObject.GetNamedChild("Button Front").GetNamedChild("Text (TMP) ").GetComponent<TextMeshProUGUI>();
        mirrorButtonText.text = "Stop Mirroring";
    }

    public void stopMirroring()
    {
        mirrorInputState = false;
        TextMeshProUGUI mirrorButtonText = mirrorButtonObject.GetNamedChild("Button Front").GetNamedChild("Text (TMP) ").GetComponent<TextMeshProUGUI>();
        mirrorButtonText.text = "Start Mirroring";
    }

    void MirrorStateCallback(JointStateMsg jointState)
    {
        if (mirrorInputState)
        {
            for (int i = 0; i < knobs.Count; i++)
            {
                knobs[i].transform.localRotation = Quaternion.Euler(0, (float)jointState.position[i], 0);
            }
        }
    }

    void PublishState()
    {
        if (publishState)
        {
            // Update the joint positions
            for (int i = 0; i < knobs.Count; i++)
            {
                jointPositions[i] = knobs[i].transform.localRotation.eulerAngles.y;
            }

            // Publish the joint state message
            JointStateMsg jointState = new JointStateMsg
            {
                name = jointNames.ToArray(),
                position = jointPositions.ToArray()
            };
            ros.Publish(outputStateTopicName, jointState);
        }
    }

    void LoadQueryInterface()
    {
        Debug.Log("LoadQueryInterface");
        if (queryUI == null)
        {
            Debug.LogError("error loading UI");
        }
        queryUI = Instantiate(queryUI, transform);
        GameObject contentGameObject = queryUI.GetNamedChild("Spatial Panel Scroll").GetNamedChild("Scroll View").GetNamedChild("Viewport").GetNamedChild("Content");

        startButtonObject = contentGameObject.GetNamedChild("Set Start Button").GetNamedChild("Text Poke Button");
        Button startButton = startButtonObject.GetComponent<Button>();
        TextMeshProUGUI startButtonText = startButtonObject.GetNamedChild("Button Front").GetNamedChild("Text (TMP) ").GetComponent<TextMeshProUGUI>();

        startButton.onClick.AddListener(() =>
        {
            setStart();
            startButtonText.text = "Start is Set!";
        });

        goalButtonObject = contentGameObject.GetNamedChild("Set Goal Button").GetNamedChild("Text Poke Button");
        Button goalButton = goalButtonObject.GetComponent<Button>();
        TextMeshProUGUI goalButtonText = goalButtonObject.GetNamedChild("Button Front").GetNamedChild("Text (TMP) ").GetComponent<TextMeshProUGUI>();

        goalButton.onClick.AddListener(() =>
        {
            setGoal();
            goalButtonText.text = "Goal is Set!";
        });

        queryButtonObject = contentGameObject.GetNamedChild("Send Query Button").GetNamedChild("Text Poke Button");
        Button queryButton = queryButtonObject.GetComponent<Button>();
        TextMeshProUGUI queryButtonText = queryButtonObject.GetNamedChild("Button Front").GetNamedChild("Text (TMP) ").GetComponent<TextMeshProUGUI>();

        queryButton.onClick.AddListener(() =>
        {
            sendQueryMessage();
            queryButtonText.text = "Query Sent!";
        });

        // Disable the query button until both start and goal are set
        queryButton.interactable = false;
    }

    void setStart()
    {
        startPositions = new List<double>();
        for (int i = 0; i < knobs.Count; i++)
        {
            startPositions.Add(knobs[i].transform.localRotation.eulerAngles.y);
        }

        CheckQueryButton();
    }

    void setGoal()
    {
        goalPositions = new List<double>();
        for (int i = 0; i < knobs.Count; i++)
        {
            goalPositions.Add(knobs[i].transform.localRotation.eulerAngles.y);
        }

        CheckQueryButton();
    }

    void CheckQueryButton()
    {
        if (startPositions != null && goalPositions != null)
        {
            Debug.Log("Both start and goal are set");

            // Enable the query send button
            Button queryButton = queryButtonObject.GetComponent<Button>();
            queryButton.interactable = true;
        }
    }

    void sendQueryMessage()
    {
        JointStateMsg start = new JointStateMsg
        {
            name = jointNames.ToArray(),
            position = startPositions.ToArray()
        };

        JointStateMsg goal = new JointStateMsg
        {
            name = jointNames.ToArray(),
            position = goalPositions.ToArray()
        };

        JointQueryMsg jointQuery = new JointQueryMsg
        {
            start = start,
            goal = goal
        };
        ros.Publish(queryTopicName, jointQuery);

        // Also enable the record button
        Button recordButton = recordButtonObject.GetComponent<Button>();
        recordButton.interactable = true;
    }

    void LoadUI()
    {
        if (robotUI == null)
        {
            Debug.LogError("error loading UI");
        }
        robotUI = Instantiate(robotUI, transform);
        GameObject contentGameObject = robotUI.GetNamedChild("Spatial Panel Scroll").GetNamedChild("Scroll View").GetNamedChild("Viewport").GetNamedChild("Content");

        // button
        recordButtonObject = contentGameObject.GetNamedChild("List Item Button").GetNamedChild("Text Poke Button");
        Button button = recordButtonObject.GetComponent<Button>();
        TextMeshProUGUI buttonText = recordButtonObject.GetNamedChild("Button Front").GetNamedChild("Text (TMP) ").GetComponent<TextMeshProUGUI>();

        button.onClick.AddListener(() =>
        {
            if (recordROS == true)
            {
                recordROS = false;
                buttonText.text = "Start Recording";
                sendJointPositionMessage();
            }
            else
            {
                recordROS = true;
                buttonText.text = "Send Recording";
            }
        });

        // Disable the button until a query is sent
        button.interactable = false;

        // dropdown and slider
        TMP_Dropdown dropdown = contentGameObject.GetNamedChild("List Item Dropdown").GetNamedChild("Dropdown").GetComponent<TMP_Dropdown>();
        Slider slider = contentGameObject.GetNamedChild("List Item Slider").GetNamedChild("MinMax Slider").GetComponent<Slider>();
        TextMeshProUGUI sliderText = slider.gameObject.GetNamedChild("Value Text").GetComponent<TextMeshProUGUI>();

        dropdown.AddOptions(jointNames);
        int dropdownIndex = 0;
        slider.value = knobs[dropdownIndex].GetComponentInParent<XRKnobAlt>().value;
        sliderText.text = knobs[dropdownIndex].transform.localRotation.eulerAngles.y.ToString();

        dropdown.onValueChanged.AddListener(delegate
        {
            dropdownIndex = dropdown.value;
            slider.value = knobs[dropdown.value].GetComponentInParent<XRKnobAlt>().value;
        });

        slider.onValueChanged.AddListener(delegate
        {
            knobs[dropdownIndex].GetComponentInParent<XRKnobAlt>().value = slider.value;
            sliderText.text = knobs[dropdownIndex].transform.localRotation.eulerAngles.y.ToString();
        });

        InvokeRepeating("addJointPosition", 1.0f, recordInterval);
    }

    void addJointPosition()
    {
        if (recordROS)
        {
            if (recordStartTime == 0)
            {
                recordStartTime = (int)Time.time;
            }

            jointTorques = new List<double>();
            for (int i = 0; i < knobs.Count; i++)
            {
                jointPositions[i] = knobs[i].transform.localRotation.eulerAngles.y;

                // Get the torque from the knob
                float torque = CalculateTorque(knobs[i]);
                jointTorques.Add(torque);
            }

            JointTrajectoryPointMsg jointTrajectoryPoint = new JointTrajectoryPointMsg
            {
                positions = jointPositions.ToArray(),
                effort = jointTorques.ToArray(),
                time_from_start = new DurationMsg((int)Time.time - recordStartTime, 0),
            };
            jointTrajectoryPoints.Add(jointTrajectoryPoint);
        }
    }

    float CalculateTorque(Transform knob)
    {
        float currentAngle = knob.transform.localEulerAngles.y;
        float previousAngle = previousAngles[knob];
        float deltaAngle = Mathf.DeltaAngle(previousAngle, currentAngle);
        float angularVelocity = deltaAngle / Time.deltaTime;
        float torque = momentsOfInertia[knob] * angularVelocity;
        previousAngles[knob] = currentAngle;
        return torque;
    }

    void sendJointPositionMessage()
    {
        JointTrajectoryMsg jointTrajectory = new JointTrajectoryMsg();

        HeaderMsg header = new HeaderMsg
        {
            frame_id = gameObject.name,
            stamp = new TimeMsg
            {
                sec = (int)Time.time,
                nanosec = (uint)((Time.time - (int)Time.time) * 1e9)
            }
        };
        jointTrajectory.header = header;
        jointTrajectory.joint_names = jointNames.ToArray();
        jointTrajectory.points = jointTrajectoryPoints.ToArray();
        ros.Publish(trajTopicName, jointTrajectory);

        // Clear the jointTrajectoryPoints list
        jointTrajectoryPoints.Clear();
        recordStartTime = 0;
    }
}