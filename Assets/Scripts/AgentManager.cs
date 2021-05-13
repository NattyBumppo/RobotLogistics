using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public struct AgentData
{
    public Color color;
    public string preferredName;
    public string currentStatus;
    public string hostname;
    public int port;
    public Vector3 latestPosition;
    public GameObject go;
    public int currentTaskID;
    public RenderTexture renderTextureForCamera;
    public DateTime datetimeOfLastMessage;
    public int tasksCompleted;
    public int lastNodeIdxVisited;
    public int idxInAgentsList;
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

    public Text agentCountText;
    public int agentCount;
    public float timeInSecondsBeforeAgentIsStale;

    // Allow for asynchronous request and implementation of agent creation
    public Color requestedColorForAgentCreation;
    public string requestedHostnameForAgentCreation;
    public int requestedPortForAgentCreation;
    public string requestedPreferredNameForAgentCreation;
    public bool agentCreationRequestIssued;

    // Allow for asynchronous agent position updates
    public int requestedStartNodeGraphIdxForAgentPositionUpdate;
    public int requestedEndNodeGraphIdxForAgentPositionUpdate;
    public float requestedFractionForAgentPositionUpdate;
    public string requestedPreferredNameForAgentPositionUpdate;
    public bool agentPositionUpdateRequestIssued;

    // Allow for asynchronous request and implementation of agent destruction
    public string requestedPreferredNameForAgentDestruction;
    public bool agentDestructionRequestIssued;

    // Allow to asynchronous request and implementation of agent status message updates
    public string requestedStatusMessageForAgentStatusUpdate;
    public string requestedPreferredNameForAgentStatusUpdate;
    public bool agentStatusUpdateRequestIssued;

    void UpdateAgentCountText()
    {
        agentCountText.text = agentCount == 1 ? "Active Agent: " + agentCount.ToString() : "Active Agents: " + agentCount.ToString();
    }

    public void RequestAgentCreation(Color color, string hostname, int port, string preferredName)
    {
        requestedColorForAgentCreation = color;
        requestedHostnameForAgentCreation = hostname;
        requestedPortForAgentCreation = port;
        requestedPreferredNameForAgentCreation = preferredName;

        agentCreationRequestIssued = true;
    }

    public void RequestAgentPositionUpdate(int startNodeGraphIdx, int endNodeGraphIdx, float fraction, string preferredName)
    {
        Debug.Log("Moving agent to fraction " + fraction + " between " + startNodeGraphIdx + " and " + endNodeGraphIdx);

        requestedStartNodeGraphIdxForAgentPositionUpdate = startNodeGraphIdx;
        requestedEndNodeGraphIdxForAgentPositionUpdate = endNodeGraphIdx;
        requestedFractionForAgentPositionUpdate = fraction;
        requestedPreferredNameForAgentPositionUpdate = preferredName;

        agentPositionUpdateRequestIssued = true;
    }

    public void RequestAgentDestruction(string preferredName)
    {
        requestedPreferredNameForAgentDestruction = preferredName;

        agentDestructionRequestIssued = true;
    }

    public void RequestAgentStatusUpdate(string statusMessage, string preferredName)
    {
        requestedStatusMessageForAgentStatusUpdate = statusMessage;
        requestedPreferredNameForAgentStatusUpdate = preferredName;

        agentStatusUpdateRequestIssued = true;
    }

    //Vector3 GetRandomPosition()
    //{
    //    return new Vector3(UnityEngine.Random.Range(-1.0f, 1.0f), UnityEngine.Random.Range(-1.0f, 1.0f), UnityEngine.Random.Range(-1.0f, 1.0f));
    //}

    public bool GetAgentByName(string preferredName, out AgentData agent)
    {
        agent = new AgentData();

        foreach (AgentData existingAd in agents)
        {
            if (existingAd.preferredName == preferredName)
            {
                agent = existingAd;
                return true;
            }
        }

        return false;
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
        ad.currentStatus = "(Idle)";

        GraphNode initialNode = mm.GetRandomUnoccupiedNode();

        ad.latestPosition = initialNode.pos;
        ad.lastNodeIdxVisited = mm.globalIdxToGraphIdx[initialNode.globalIdx];

        // Create visual representation for agent
        GameObject go = Instantiate(agentPrefab, initialNode.pos, Quaternion.identity);
        ad.go = go;
        go.name = ad.preferredName;
        go.transform.SetParent(agentParent);

        // Set up text labels
        foreach (TextMeshPro tm in go.GetComponentsInChildren<TextMeshPro>())
        {
            if (tm.gameObject.name == "Name")
            {
                tm.text = ad.preferredName;
            }
            else if (tm.gameObject.name == "Status")
            {
                tm.text = ad.currentStatus;
            }

            tm.outlineColor = color;
        }

        // Color as specified (make a bit lighter to account for the unlit material
        // for the gameObject
        Color goColor = new Color(Mathf.Clamp(color.r + 0.5f, 0.0f, 1.0f), Mathf.Clamp(color.g + 0.5f, 0.0f, 1.0f), Mathf.Clamp(color.b + 0.5f, 0.0f, 1.0f));
        go.GetComponent<MeshRenderer>().material.color = goColor;

        // No current task
        ad.currentTaskID = -1;
        ad.datetimeOfLastMessage = DateTime.UtcNow;
        ad.tasksCompleted = 0;

        // Make a render texture for the agent to draw to
        RenderTexture rt = new RenderTexture(baseRenderTexture);
        ad.renderTextureForCamera = rt;
        go.GetComponentInChildren<Camera>().targetTexture = rt;

        ad.idxInAgentsList = agentCount;

        // Add to agents list so that we can keep track of it
        agents.Add(ad);

        agentCount++;
        UpdateAgentCountText();

        return true;
    }

    public bool DestroyAgent(int indexInAgentsList)
    {
        if (indexInAgentsList < agents.Count)
        {
            AgentData ad = agents[indexInAgentsList];

            // Remove agent from agents list and remove corresponding GameObject
            ad.go.GetComponentInChildren<Camera>().targetTexture = null;
            Destroy(ad.renderTextureForCamera);
            Destroy(ad.go);
            agents.RemoveAt(indexInAgentsList);

            agentCount--;
            UpdateAgentCountText();

            return true;
        }
        else
        {
            return false;
        }
    }

    public bool DestroyAgent(string preferredName)
    {
        AgentData ad;

        // Look for agent with this name
        bool agentFound = GetAgentByName(preferredName, out ad);

        // Return an error if no agent exists with this name
        if (!agentFound)
        {
            return false;
        }
        else
        {
            // Remove agent from agents list and remove corresponding GameObject
            ad.go.GetComponentInChildren<Camera>().targetTexture = null;
            Destroy(ad.renderTextureForCamera);
            Destroy(ad.go);
            agents.RemoveAt(ad.idxInAgentsList);

            agentCount--;
            UpdateAgentCountText();
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

    //public void UpdateAgentPosition(string agentPreferredName, Vector3 newPosition)
    //{
    //    foreach (AgentData ad in agents)
    //    {
    //        if (ad.preferredName == agentPreferredName)
    //        {
    //            ad.go.transform.position = newPosition;
    //            return;
    //        }
    //    }

    //    Debug.LogError("Error: could not update position for " + agentPreferredName + " (didn't find agent by that name)");
    //}

    public void UpdateAgentPosition(string agentPreferredName, int startNodeGraphIdx, int endNodeGraphIdx, float fraction)
    {
        Vector3 startPos = mm.GetNode(startNodeGraphIdx).pos;
        Vector3 endPos = mm.GetNode(endNodeGraphIdx).pos;

        Vector3 newPos = Vector3.Lerp(startPos, endPos, fraction);

        AgentData ad;
        bool agentFound = GetAgentByName(agentPreferredName, out ad);

        if (agentFound)
        {
            ad.latestPosition = newPos;
            ad.go.transform.position = newPos;

            ad.datetimeOfLastMessage = DateTime.UtcNow;
        }
        else
        {
            Debug.LogError("Error: could not update position for agent " + agentPreferredName + " due to agent not existing!");
        }
    }

    void AssignTaskTo(int taskID, int agentID)
    {

    }

    void UpdateStatusMessage(string agentPreferredName, string statusMessage)
    {
        AgentData ad;
        bool agentFound = GetAgentByName(agentPreferredName, out ad);

        if (agentFound)
        {
            foreach (TextMeshPro tm in ad.go.GetComponentsInChildren<TextMeshPro>())
            {
                if (tm.gameObject.name == "Status")
                {
                    ad.currentStatus = statusMessage;
                    tm.text = ad.currentStatus;
                }
            }

            ad.datetimeOfLastMessage = DateTime.UtcNow;
        }
        else
        {
            Debug.LogError("Error: could not update status for agent " + agentPreferredName + " due to agent not existing!");
        }
    }

    public void PublicStart()
    {
        agentCreationRequestIssued = false;
        agentDestructionRequestIssued = false;
        agentPositionUpdateRequestIssued = false;

        agentCount = 0;
        UpdateAgentCountText();
    }

    // Remove agents that haven't been heard from in a while
    private void CleanOutStaleAgents()
    {
        for (int i = agents.Count-1; i >=0; i--)
        {
            if (DateTime.UtcNow.Subtract(agents[i].datetimeOfLastMessage).TotalSeconds > timeInSecondsBeforeAgentIsStale)
            {
                DestroyAgent(i);
            }
        }
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

        if (agentPositionUpdateRequestIssued)
        {
            UpdateAgentPosition(requestedPreferredNameForAgentPositionUpdate, requestedStartNodeGraphIdxForAgentPositionUpdate, requestedEndNodeGraphIdxForAgentPositionUpdate, requestedFractionForAgentPositionUpdate);

            agentPositionUpdateRequestIssued = false;
        }

        if (agentDestructionRequestIssued)
        {
            DestroyAgent(requestedPreferredNameForAgentCreation);

            agentDestructionRequestIssued = false;
        }

        if (agentStatusUpdateRequestIssued)
        {
            UpdateStatusMessage(requestedPreferredNameForAgentStatusUpdate, requestedStatusMessageForAgentStatusUpdate);

            agentStatusUpdateRequestIssued = false;
        }

        CleanOutStaleAgents();
    }
}
