using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct GraphNode
{
    public int myIndex;
    public List<int> neighborIndices;
    public Vector3 myPos;
}

public class MapManager : MonoBehaviour
{
    public float minX;
    public float maxX;
    public float defaultY;
    public float minZ;
    public float maxZ;
    public int numNodesHorizontal;
    public int numNodesVertical;

    public GameObject nodePrefab;
    public GameObject nodeConnectorPrefab;
    public int nodeConnectorsPerConnection;

    public Transform nodeParent;
    public Transform nodeConnectorParent;

    List<GraphNode> graph = new List<GraphNode>();
    List<GameObject> nodeConnectors = new List<GameObject>();

    void AddConnector(int graphIdx0, int graphIdx1)
    {
        Vector3 startPos = graph[graphIdx0].myPos;
        Vector3 endPos = graph[graphIdx1].myPos;

        float fractionalIncrease = 1.0f / (float)(nodeConnectorsPerConnection + 1);

        for (int i = 1; i <= nodeConnectorsPerConnection; i++)
        {
            Vector3 connectorPos = Vector3.Lerp(startPos, endPos, fractionalIncrease*i);
            GameObject go = Instantiate(nodeConnectorPrefab, connectorPos, Quaternion.identity);
            go.transform.SetParent(nodeConnectorParent);

            nodeConnectors.Add(go);
        }
    }

    void SetUpMap()
    {
        float xInterval = (maxX - minX) / (numNodesHorizontal-1);
        float zInterval = (maxZ - minZ) / (numNodesVertical-1);

        int nodeCount = 0;

        // Add nodes
        for (int xIdx = 0; xIdx < numNodesHorizontal; xIdx++)
        {
            for (int zIdx = 0; zIdx < numNodesVertical; zIdx++)
            {
                Vector3 nodePos = new Vector3(minX + xIdx * xInterval, defaultY, minZ + zIdx * zInterval);
                GameObject go = Instantiate(nodePrefab, nodePos, Quaternion.identity);
                go.transform.SetParent(nodeParent);

                GraphNode node = new GraphNode();
                node.myIndex = nodeCount;
                node.neighborIndices = new List<int>();
                node.myPos = nodePos;

                graph.Add(node);

                nodeCount++;
            }
        }

        AddConnector(0, 1);
        AddConnector(1, 2);
        AddConnector(3, 4);


    }
    
    // Start is called before the first frame update
    void Start()
    {
        SetUpMap();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
