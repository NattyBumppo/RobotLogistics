using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public struct AgentData
{
    public Color color;
    public string preferredName;
    public string hostname;
    public int port;
    public Vector3 latestPosition;
    public GameObject go;
    public int currentTaskID;
    public RenderTexture renderTextureForCamera;
    public DateTime datetimeOfLastMessage;
    public int tasksCompleted;
    public int lastNodeIdxVisited;
}

public struct DeliveryTask
{
    int destination;
}

public class AgentManager : MonoBehaviour
{
    public MapManager mm;

    public List<AgentData> agents = new List<AgentData>();
    public GameObject agentPrefab;
    public Transform agentParent;
    public RenderTexture baseRenderTexture;

    // Allow for asynchronous request and implementation of agent creation
    public Color requestedColorForAgentCreation;
    public string requestedHostnameForAgentCreation;
    public int requestedPortForAgentCreation;
    public string requestedPreferredNameForAgentCreation;
    public bool agentCreationRequestIssued;

    // Allow for asynchronous request and implementation of agent destruction
    public string requestedHostnameForAgentDestructionn;
    public int requestedPortForAgentDestruction;
    public string requestedPreferredNameForAgentDestruction;
    public bool agentDestructionRequestIssued;

    public void RequestAgentCreation(Color color, string hostname, int port, string preferredName)
    {
        requestedColorForAgentCreation = color;
        requestedHostnameForAgentCreation = hostname;
        requestedPortForAgentCreation = port;
        requestedPreferredNameForAgentCreation = preferredName;

        agentCreationRequestIssued = true;
    }

    public void RequestAgentDestruction(string hostname, int port, string preferredName)
    {
        requestedHostnameForAgentDestructionn = hostname;
        requestedPortForAgentDestruction = port;
        requestedPreferredNameForAgentDestruction = preferredName;

        agentDestructionRequestIssued = true;
    }

    Vector3 GetRandomPosition()
    {
        return new Vector3(UnityEngine.Random.Range(-1.0f, 1.0f), UnityEngine.Random.Range(-1.0f, 1.0f), UnityEngine.Random.Range(-1.0f, 1.0f));
    }

    public bool CreateAgent(Color color, string hostname, int port, string preferredName)
    {
        // Don't allow agent to be created if preferred name is not unique
        foreach (AgentData existingAd in agents)
        {
            if (existingAd.preferredName == preferredName)
            {
                Debug.LogError("Error: could not add new agent named " + preferredName + " (preferred name already taken)");

                return false;
            }
        }

        // Create object to store agent's data
        AgentData ad = new AgentData();
        ad.color = color;
        ad.hostname = hostname;
        ad.port = port;
        ad.preferredName = preferredName;

        GraphNode initialNode = mm.GetRandomUnoccupiedNode();

        ad.latestPosition = initialNode.pos;
        ad.lastNodeIdxVisited = initialNode.globalIndex;

        // Create visual representation for agent
        GameObject go = Instantiate(agentPrefab, initialNode.pos, Quaternion.identity);
        ad.go = go;
        go.name = ad.preferredName;
        go.transform.SetParent(agentParent);

        // Set up text label
        TextMeshPro tm = go.GetComponentInChildren<TextMeshPro>();
        tm.text = ad.preferredName;
        tm.outlineColor = color;

        // Color as specified
        go.GetComponent<MeshRenderer>().material.SetColor("_EmissionColor", color);

        // No current task
        ad.currentTaskID = -1;
        ad.datetimeOfLastMessage = DateTime.UtcNow;
        ad.tasksCompleted = 0;

        // Make a render texture for the agent to draw to
        RenderTexture rt = new RenderTexture(baseRenderTexture);
        ad.renderTextureForCamera = rt;
        go.GetComponentInChildren<Camera>().targetTexture = rt;

        // Add to agents list so that we can keep track of it
        agents.Add(ad);

        return true;
    }

    public bool DestroyAgent(string hostname, int port, string preferredName)
    {
        int agentIdx = -1;

        // Look for agent with this name
        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i].preferredName == preferredName)
            {
                agentIdx = i;
                break;
            }
        }

        // Return an error if no agent exists with this name
        if (agentIdx == -1)
        {
            return false;
        }
        else
        {
            // Remove agent from agents list and remove corresponding GameObject
            agents[agentIdx].go.GetComponentInChildren<Camera>().targetTexture = null;
            Destroy(agents[agentIdx].renderTextureForCamera);
            Destroy(agents[agentIdx].go);
            agents.RemoveAt(agentIdx);
        }

        return true;
    }

    void TestAddingAgents()
    {
        List<Color> colors = new List<Color>() { Color.blue, Color.red, Color.green, Color.magenta, Color.cyan };

        for (int i = 0; i < 20; i++)
        {
            CreateAgent(colors[UnityEngine.Random.Range(0, colors.Count)], "host" + i, 333, "Agent " + i);
        }

        //CreateAgent(Color.blue, "host0", "Agent 0", 333, mm.GetRandomNodeInGraph().pos);
        //CreateAgent(Color.blue, "host1", "Agent 1", 333, mm.GetRandomNodeInGraph().pos);
        //CreateAgent(Color.red, "host2", "Agent 2", 333, mm.GetRandomNodeInGraph().pos);
    }

    void UpdateAgentPosition(string agentPreferredName, Vector3 newPosition)
    {
        foreach (AgentData ad in agents)
        {
            if (ad.preferredName == agentPreferredName)
            {
                ad.go.transform.position = newPosition;
                return;
            }
        }

        Debug.LogError("Error: could not update position for " + agentPreferredName + " (didn't find agent by that name)");
    }

    void AssignTaskTo(int taskID, int agentID)
    {

    }

    public void PublicStart()
    {
        agentCreationRequestIssued = false;
        agentDestructionRequestIssued = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            TestAddingAgents();
        }

        if (agentCreationRequestIssued)
        {
            CreateAgent(requestedColorForAgentCreation, requestedHostnameForAgentCreation, requestedPortForAgentCreation, requestedPreferredNameForAgentCreation);

            agentCreationRequestIssued = false;
        }

        if (agentDestructionRequestIssued)
        {
            DestroyAgent(requestedHostnameForAgentCreation, requestedPortForAgentCreation, requestedPreferredNameForAgentCreation);

            agentCreationRequestIssued = false;
        }
    }
}
