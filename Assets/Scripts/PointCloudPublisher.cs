using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using System.Collections.Generic;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;

public class PointCloudPublisher : MonoBehaviour
{
    private ROSConnection ros;
    public string topicName = "/point_cloud";
    public float samplingResolution = 0.1f; // Distance between sampled points
    public string[] includeTags; // Tags of objects to include in the point cloud

    private byte[] cachedPointCloudData;
    private uint pointCount;

    void Start()
    {
        Debug.Log("PointCloudPublisher started");
        ros = ROSConnection.GetOrCreateInstance();
        Debug.Log("ROSConnection initialized");
        ros.RegisterPublisher<PointCloud2Msg>(topicName);
        Debug.Log("Publisher registered");
        CacheAndPublishPointCloud();
        Debug.Log("Point cloud published");

        // Publish point cloud every 5 seconds
        InvokeRepeating("PublishCachedPointCloud", 5.0f, 5.0f);
    }

    void CacheAndPublishPointCloud()
    {
        List<Vector3> pointList = new List<Vector3>();

        foreach (string tag in includeTags)
        {
            Debug.Log("Processing tag: " + tag);
            GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
            Debug.Log("Found " + objects.Length + " objects with tag " + tag);
            foreach (GameObject obj in objects)
            {
                MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    Mesh mesh = meshFilter.mesh;
                    Transform meshTransform = meshFilter.transform;

                    for (int i = 0; i < mesh.triangles.Length; i += 3)
                    {
                        Vector3 v0 = meshTransform.TransformPoint(mesh.vertices[mesh.triangles[i]]);
                        Vector3 v1 = meshTransform.TransformPoint(mesh.vertices[mesh.triangles[i + 1]]);
                        Vector3 v2 = meshTransform.TransformPoint(mesh.vertices[mesh.triangles[i + 2]]);

                        Debug.Log($"Transformed vertices: v0={v0}, v1={v1}, v2={v2}");

                        for (float u = 0; u < 1.0f; u += samplingResolution)
                        {
                            for (float v = 0; u + v < 1.0f; v += samplingResolution)
                            {
                                Vector3 sampledPoint = (1 - u - v) * v0 + u * v1 + v * v2;
                                pointList.Add(ConvertToROSCoordinateSystem(sampledPoint));
                            }
                        }
                    }
                }
            }
        }

        float[] points = new float[pointList.Count * 3];
        for (int i = 0; i < pointList.Count; i++)
        {
            points[i * 3] = pointList[i].x;
            points[i * 3 + 1] = pointList[i].y;
            points[i * 3 + 2] = pointList[i].z;
        }

        pointCount = (uint)pointList.Count;
        cachedPointCloudData = new byte[points.Length * 4];
        System.Buffer.BlockCopy(points, 0, cachedPointCloudData, 0, cachedPointCloudData.Length);

        PublishCachedPointCloud();
    }

    Vector3 ConvertToROSCoordinateSystem(Vector3 unityPoint)
    {
        // Swap Y and Z, and invert Z
        return new Vector3(unityPoint.x, unityPoint.z, -unityPoint.y);
    }

    void PublishCachedPointCloud()
    {
        Debug.Log("Publishing point cloud with " + pointCount + " points");
        List<PointFieldMsg> fields = new List<PointFieldMsg>
        {
            new PointFieldMsg { name = "x", offset = 0, datatype = PointFieldMsg.FLOAT32, count = 1 },
            new PointFieldMsg { name = "y", offset = 4, datatype = PointFieldMsg.FLOAT32, count = 1 },
            new PointFieldMsg { name = "z", offset = 8, datatype = PointFieldMsg.FLOAT32, count = 1 }
        };

        PointCloud2Msg pointCloud = new PointCloud2Msg
        {
            header = new HeaderMsg
            {
                frame_id = "map",
                stamp = new TimeMsg
                {
                    sec = (int)Time.time,
                    nanosec = (uint)((Time.time - (int)Time.time) * 1e9)
                }
            },
            height = 1,
            width = pointCount,
            fields = fields.ToArray(),
            is_bigendian = false,
            point_step = 12,
            row_step = (uint)(pointCount * 12),
            data = cachedPointCloudData
        };

        Debug.Log("Publishing point cloud");
        ros.Publish(topicName, pointCloud);
        Debug.Log("Point cloud published 0");
    }
}
