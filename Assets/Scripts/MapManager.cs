using System.Collections.Generic;
using UnityEngine;

public struct GraphNode
{
    public int globalIndex;
    public int horizIdx;
    public int vertIdx;
    public List<int> connectedNeighborIndices;
    public Vector3 pos;
    public GameObject go;
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

    public int randomConnectionsToAddOnInit;

    public GameObject nodePrefab;
    public GameObject nodeConnectorPrefab;
    public int nodeConnectorsPerConnection;

    public Transform nodeParent;
    public Transform nodeConnectorParent;

    List<GraphNode> graph = new List<GraphNode>();
    List<GameObject> nodeConnectors = new List<GameObject>();

    bool ConnectNodes(int graphIdx0, int graphIdx1, bool skipConnectionIfCrowded)
    {
        // Skip if already connected
        if ((graph[graphIdx0].connectedNeighborIndices.Contains(graphIdx1)) || (graph[graphIdx1].connectedNeighborIndices.Contains(graphIdx0)))
        {
            return false;
        }

        if (skipConnectionIfCrowded)
        {
            if (graph[graphIdx0].connectedNeighborIndices.Count >= 3 || graph[graphIdx1].connectedNeighborIndices.Count >= 3)
            {
                return false;
            }
        }

        Vector3 startPos = graph[graphIdx0].pos;
        Vector3 endPos = graph[graphIdx1].pos;

        float fractionalIncrease = 1.0f / (float)(nodeConnectorsPerConnection + 1);

        for (int i = 1; i <= nodeConnectorsPerConnection; i++)
        {
            Vector3 connectorPos = Vector3.Lerp(startPos, endPos, fractionalIncrease*i);
            GameObject go = Instantiate(nodeConnectorPrefab, connectorPos, Quaternion.identity);
            go.transform.SetParent(nodeConnectorParent);

            nodeConnectors.Add(go);
        }

        graph[graphIdx0].connectedNeighborIndices.Add(graphIdx1);
        graph[graphIdx1].connectedNeighborIndices.Add(graphIdx0);

        return true;
    }

    bool ConnectNodes(GraphNode gn0, GraphNode gn1, bool skipConnectionIfCrowded)
    {
        return ConnectNodes(gn0.globalIndex, gn1.globalIndex, skipConnectionIfCrowded);
    }

    int GetGlobalIndexFromCoordinates(int horizIdx, int vertIdx)
    {
        return vertIdx * numNodesHorizontal + horizIdx;
    }

    List<GraphNode> GetPossibleNeighbors(GraphNode node)
    {
        List<GraphNode> possibleNeighbors = new List<GraphNode>();

        // Check for neighbor to the left
        if (node.horizIdx > 0)
        {
            int neighborIdx = GetGlobalIndexFromCoordinates(node.horizIdx-1, node.vertIdx);
            possibleNeighbors.Add(graph[neighborIdx]);
        }

        // Check for neighbor to the right
        if (node.horizIdx < numNodesHorizontal-1)
        {
            int neighborIdx = GetGlobalIndexFromCoordinates(node.horizIdx+1, node.vertIdx);
            possibleNeighbors.Add(graph[neighborIdx]);
        }

        // Check for neighbor below
        if (node.vertIdx > 0)
        {
            int neighborIdx = GetGlobalIndexFromCoordinates(node.horizIdx, node.vertIdx-1);
            possibleNeighbors.Add(graph[neighborIdx]);
        }

        // Check for neighbor above
        if (node.vertIdx < numNodesVertical-1)
        {
            int neighborIdx = GetGlobalIndexFromCoordinates(node.horizIdx, node.vertIdx+1);
            possibleNeighbors.Add(graph[neighborIdx]);
        }

        return possibleNeighbors;
    }

    void SetUpMap()
    {
        float xInterval = (maxX - minX) / (numNodesHorizontal-1);
        float zInterval = (maxZ - minZ) / (numNodesVertical-1);

        int nodeCount = 0;

        // Add nodes
        for (int vertIdx = 0; vertIdx < numNodesVertical; vertIdx++)
        {
            for (int horizIdx = 0; horizIdx < numNodesHorizontal; horizIdx++)
            {
                Vector3 nodePos = new Vector3(minX + horizIdx * xInterval, defaultY, minZ + vertIdx * zInterval);
                GameObject go = Instantiate(nodePrefab, nodePos, Quaternion.identity);
                go.transform.SetParent(nodeParent);

                GraphNode node = new GraphNode();
                node.globalIndex = nodeCount;
                node.connectedNeighborIndices = new List<int>();
                node.pos = nodePos;
                node.go = go;
                node.horizIdx = horizIdx;
                node.vertIdx = vertIdx;

                graph.Add(node);

                nodeCount++;
            }
        }

        GenerateRandomConnections();
        HideUnconnectedNodes();
    }
    
    void GenerateRandomConnections()
    {
        int startIdxHoriz = numNodesHorizontal / 2;
        int startIdxVert = numNodesVertical / 2;
        int startIdxGlobal = GetGlobalIndexFromCoordinates(startIdxHoriz, startIdxVert);

        List<GraphNode> nodesAddedSoFar = new List<GraphNode> { graph[startIdxGlobal] };

        int connectionsMade = 0;

        while(connectionsMade < randomConnectionsToAddOnInit)
        {
            // Pick a random node in the nodes added so far
            GraphNode newNode = nodesAddedSoFar[Random.Range(0, nodesAddedSoFar.Count)];

            // Get its neighbors
            List<GraphNode> neighbors = GetPossibleNeighbors(newNode);

            // Pick a random neighbor
            GraphNode neighborToConnect = neighbors[Random.Range(0, neighbors.Count)];

            // Connect the two nodes, often (but not always) skipping when crowded,
            // to reduce the number of four-way intersections
            bool skipIfCrowded = Random.value > 0.01f ? true : false;

            bool connectionSuccessful = ConnectNodes(newNode, neighborToConnect, skipIfCrowded);

            if (connectionSuccessful)
            {
                connectionsMade++;

                if (!nodesAddedSoFar.Contains(neighborToConnect))
                {
                    nodesAddedSoFar.Add(neighborToConnect);
                }
            }
        }

    }

    void TestConnectingNeighbors()
    {
        // Test connecting nodes to neighbors
        List<int> testNodeIndices = new List<int> { 1, 10, 20, 30, graph.Count - 1 };

        for (int i = 0; i < testNodeIndices.Count; i++)
        {
            int testNodeIdx = testNodeIndices[i];

            List<GraphNode> neighbors = GetPossibleNeighbors(graph[testNodeIdx]);

            for (int n = 0; n < neighbors.Count; n++)
            {
                ConnectNodes(neighbors[n].globalIndex, testNodeIdx, false);
            }
        }
    }

    void HideUnconnectedNodes()
    {
        for (int i = 0; i < graph.Count; i++)
        {
            if (graph[i].connectedNeighborIndices.Count == 0)
            {
                graph[i].go.SetActive(false);
            }
        }
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
