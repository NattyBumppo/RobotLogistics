using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum AgentType
{
    FAST_LOAD_SLOW_MOVE,
    SLOW_LOAD_FAST_MOVE
}

public struct AgentData
{
    public Color color;
    public AgentType agentType;
    public string preferredName;
    public string currentStatus;
    public string hostname;
    public int port;
    public Vector3 latestPosition;
    public GameObject go;
    public string currentTaskGUID;
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
    public TaskManager tm;

    public List<AgentData> agents = new List<AgentData>();
    public GameObject agentPrefab;
    public Transform agentParent;

    public Text agentCountText;
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
        agentCountText.text = agents.Count == 1 ? "Active Agent: " + agents.Count.ToString() : "Active Agents: " + agents.Count.ToString();
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
        //Debug.Log("Moving agent to fraction " + fraction + " between " + startNodeGraphIdx + " and " + endNodeGraphIdx);

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


    public bool GetAgentIdxByName(string preferredName, out int agentIdx)
    {
        agentIdx = -1;

        for (int i = 0; i < agents.Count; i++)
        {
            if (agents[i].preferredName == preferredName)
            {
                agentIdx = i;
                return true;
            }
        }

        return false;
    }

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

    public float GetAgentDistanceNeglectingVerticalHeight(AgentData ad, Vector3 pos)
    {
        Vector2 adVec2 = new Vector2(ad.latestPosition.x, ad.latestPosition.z);
        Vector2 posVec2 = new Vector2(pos.x, pos.z);

        return Vector2.Distance(adVec2, posVec2);
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

        // Agent type is hard-coded for now based on color
        //
        // Blue: slow load, fast move
        // Red: fast load, slow move
        //
        if (ad.color == new Color(1.0f, 0.0f, 0.0f))
        {
            ad.agentType = AgentType.FAST_LOAD_SLOW_MOVE;
        }
        else
        {
            ad.agentType = AgentType.SLOW_LOAD_FAST_MOVE;
        }

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
        ad.currentTaskGUID = "";
        ad.datetimeOfLastMessage = DateTime.UtcNow;
        ad.tasksCompleted = 0;

        // Add to agents list so that we can keep track of it
        agents.Add(ad);

        UpdateAgentCountText();

        return true;
    }

    public bool DestroyAgent(int indexInAgentsList)
    {
        if (indexInAgentsList < agents.Count)
        {
            AgentData ad = agents[indexInAgentsList];

            // Remove agent from agents list and remove corresponding GameObject
            Destroy(ad.go);
            agents.RemoveAt(indexInAgentsList);

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
        int agentIdx;

        // Look for agent with this name
        bool agentIdxFound = GetAgentIdxByName(preferredName, out agentIdx);

        // Return an error if no agent exists with this name
        if (!agentIdxFound)
        {
            return false;
        }
        else
        {
            // Remove agent from agents list and remove corresponding GameObject
            Destroy(agents[agentIdx].go);
            agents.RemoveAt(agentIdx);

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
    }

    public void UpdateAgentPosition(string agentPreferredName, int startNodeGraphIdx, int endNodeGraphIdx, float fraction)
    {
        Vector3 startPos = mm.GetNode(startNodeGraphIdx).pos;
        Vector3 endPos = mm.GetNode(endNodeGraphIdx).pos;

        Vector3 newPos = Vector3.Lerp(startPos, endPos, fraction);

        int agentIdx;

        // Look for agent with this name
        bool agentIdxFound = GetAgentIdxByName(agentPreferredName, out agentIdx);

        if (agentIdxFound)
        {
            AgentData ad = agents[agentIdx];

            ad.latestPosition = newPos;
            ad.go.transform.position = newPos;

            ad.datetimeOfLastMessage = DateTime.UtcNow;

            // Copy updated agent back into list
            agents[agentIdx] = ad;

            Debug.Log("Updated position of agent " + agentPreferredName + " to " + newPos);
        }
        else
        {
            Debug.LogError("Error: could not update position for agent " + agentPreferredName + " due to agent not existing!");
        }
    }

    void UpdateStatusMessage(string agentPreferredName, string statusMessage)
    {
        int agentIdx;

        // Look for agent with this name
        bool agentIdxFound = GetAgentIdxByName(agentPreferredName, out agentIdx);

        if (agentIdxFound)
        {
            AgentData ad = agents[agentIdx];

            foreach (TextMeshPro tm in ad.go.GetComponentsInChildren<TextMeshPro>())
            {
                if (tm.gameObject.name == "Status")
                {
                    ad.currentStatus = statusMessage;
                    tm.text = ad.currentStatus;
                }
            }

            ad.datetimeOfLastMessage = DateTime.UtcNow;

            // Copy updated agent back into list
            agents[agentIdx] = ad;
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

        UpdateAgentCountText();
    }

    // Remove agents that haven't been heard from in a while
    private void CleanOutStaleAgents()
    {
        for (int i = agents.Count-1; i >=0; i--)
        {
            if (DateTime.UtcNow.Subtract(agents[i].datetimeOfLastMessage).TotalSeconds > timeInSecondsBeforeAgentIsStale)
            {
                // Re-allocate any task the agent might have
                string currentTaskGUID = agents[i].currentTaskGUID;
                if (currentTaskGUID != "")
                {
                    Task taskToMove = tm.assignedTasksByGUID[currentTaskGUID];
                    tm.openTasks.Add(taskToMove);
                    tm.assignedTasksByGUID.Remove(currentTaskGUID);
                    tm.UpdateTaskCountText();
                }

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
