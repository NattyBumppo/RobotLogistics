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
}

public class AgentManager : MonoBehaviour
{
    public List<AgentData> agents = new List<AgentData>();
    public GameObject agentPrefab;
    public Transform agentParent;

    void Start()
    {
        TestAddingAgents();
    }

    bool CreateAgent(Color color, string hostname, string preferredName, int port, Vector3 initialPosition)
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

        ad.latestPosition = initialPosition;

        // Create visual representation for agent
        GameObject go = Instantiate(agentPrefab, initialPosition, Quaternion.identity);
        ad.go = go;
        go.name = ad.preferredName;
        go.transform.SetParent(agentParent);

        // Set up text label
        TextMeshPro tm = go.GetComponentInChildren<TextMeshPro>();
        tm.text = ad.preferredName;
        tm.outlineColor = color;

        // Color as specified
        go.GetComponent<MeshRenderer>().material.SetColor("_EmissionColor", color);

        // Add to agents list so that we can keep track of it
        agents.Add(ad);

        return true;
    }

    void TestAddingAgents()
    {
        CreateAgent(Color.blue, "host0", "Agent 0", 333, new Vector3(0.0f, 0.6f, 0.0f));
        CreateAgent(Color.blue, "host1", "Agent 1", 333, new Vector3(1.0f, 0.6f, 0.0f));
        CreateAgent(Color.red, "host2", "Agent 2", 333, new Vector3(-1.0f, 0.6f, 0.0f));
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

    void Update()
    {
        
    }
}
