using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct GraphNode
{
    public int globalIndex;
    public int horizIdx;
    public int vertIdx;
    public List<int> connectedNeighborIndices;
    public List<int> connectorIndices;
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

            // Start connectors as hidden
            go.SetActive(false);

            // Add reference to these connectors so that we can turn them on later
            graph[graphIdx0].connectorIndices.Add(nodeConnectors.Count);

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
                node.connectorIndices = new List<int>();
                node.pos = nodePos;
                node.go = go;
                node.horizIdx = horizIdx;
                node.vertIdx = vertIdx;

                graph.Add(node);

                nodeCount++;
            }
        }

        HideAllNodes();
        GenerateRandomConnections();
        StartCoroutine(ActivateNodesAndConnections());
    }

    void GenerateRandomConnections()
    {
        int startIdxHoriz = numNodesHorizontal / 2;
        int startIdxVert = numNodesVertical / 2;
        int startIdxGlobal = GetGlobalIndexFromCoordinates(startIdxHoriz, startIdxVert);

        List<GraphNode> nodesAddedSoFar = new List<GraphNode> { graph[startIdxGlobal] };
        graph[startIdxGlobal].go.SetActive(true);

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
                    neighborToConnect.go.SetActive(true);
                }
            }
        }
    }

    IEnumerator ActivateNodesAndConnections()
    {
        // Start at start node for graph (same as for GenerateRandomConnections())
        int startIdxHoriz = numNodesHorizontal / 2;
        int startIdxVert = numNodesVertical / 2;
        int startIdxGlobal = GetGlobalIndexFromCoordinates(startIdxHoriz, startIdxVert);

        // Starting with the start node, gradually branch out, until all of the nodes have been activated
        List<int> activatedNodeIndices = new List<int>();

        List<int> nodesToActivateIndices = new List<int> { startIdxGlobal };

        while(nodesToActivateIndices.Count > 0)
        {
            // Get next node to activate
            GraphNode nodeToActivate = graph[nodesToActivateIndices[0]];
            nodesToActivateIndices.RemoveAt(0);
            
            // Activate node
            nodeToActivate.go.SetActive(true);
            activatedNodeIndices.Add(nodeToActivate.globalIndex);

            // Activate connections
            foreach (int connectorIdx in nodeToActivate.connectorIndices)
            {
                nodeConnectors[connectorIdx].SetActive(true);
            }

            // Add neighbors to list of nodes to activate, if they're still inactive
            foreach (int neighborIdx in nodeToActivate.connectedNeighborIndices)
            {
                if (!activatedNodeIndices.Contains(neighborIdx) && !nodesToActivateIndices.Contains(neighborIdx))
                {
                    nodesToActivateIndices.Add(neighborIdx);
                }
            }

            yield return null;

        }

        Debug.Log("All nodes and connections activated!");
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

    void HideAllNodes()
    {
        for (int i = 0; i < graph.Count; i++)
        {
            graph[i].go.SetActive(false);
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
