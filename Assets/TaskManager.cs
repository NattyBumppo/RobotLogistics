using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public enum TaskType
{
    DELIVERY
}

public struct Task
{
    public TaskType taskType;
    public string name;
    public string assigneeAgentPreferredName;
    public GraphNode destinationNode;
}

public class TaskManager : MonoBehaviour
{
    public MapManager mm;
    public AgentManager am;

    public float timeBetweenTaskGenerationSeconds;
    public bool generateTasks;

    public Text openTasksText;
    public Text assignedTasksText;
    public Text completedTasksText;

    public float maxDistFromGoalForRecognition;

    public string requestedPreferredNameForTaskAssignment;
    public string mostRecentAssignedTaskGuid;
    public bool taskAssignmentRequestIssued;
    public bool taskAssignmentSuccessful;

    public string requestedPreferredNameForTaskCompletion;
    public bool taskCompletionRequestIssued;
    public bool taskCompletionSuccessful;

    public List<Task> openTasks = new List<Task>();
    public Dictionary<string, Task> assignedTasksByGUID = new Dictionary<string, Task>();
    public List<Task> completedTasks = new List<Task>();

    private List<string> foodAdjectives = new List<string>() { "acidic", "appealing", "appetizing", "aromatic", "astringent", "aromatic", "baked", "balsamic", "beautiful", "bite-size", "bitter", "bland", "blended", "boiled", "briny", "brown", "burnt", "buttered", "caked", "candied", "caramelized", "cheesy", "chocolate", "cholesterol-free", "classic", "classy", "cold", "cool", "crafted", "creamed", "creamy", "crisp", "crunchy", "cured", "dazzling", "deep-fried", "delectable", "delicious", "distinctive", "doughy", "drizzle", "dried", "extraordinary", "famous", "fantastic", "fizzy", "flaky", "flavored", "flavorful", "fluffy", "fresh", "fried", "frozen", "fruity", "garlic", "generous", "gingery", "glazed", "golden", "gorgeous", "gourmet", "greasy", "grilled", "gritty", "halal", "honey", "hot", "icy", "infused", "insipid", "intense", "juicy", "jumbo", "kosher", "large", "lavish", "lean", "low-fat", "luscious", "marinated", "mashed", "mellow", "mild", "minty", "moist", "mouth-watering", "natural", "non-fat", "nutty", "oily", "organic", "overpowering", "peppery", "petite", "pickled", "piquant", "plain", "pleasant", "plump", "poached", "prickly", "pulpy", "pungent", "pureed", "rich", "roasted", "robust", "rotten", "rubbery", "saccharine", "salty", "savory", "sapid", "saporous", "sauteed", "savory", "scrumptious", "seared", "seasoned", "silky", "simmered", "sizzling", "smelly", "smoked", "smoky", "smothered", "sour", "southern-style", "special", "spiced", "spicy", "spiral-cut", "spongy", "stale", "steamed", "sticky", "strawberry-flavored", "stuffed", "succulent", "sugar-coated", "sugar-free", "sugared", "sugarless", "sugary", "superb", "sweet", "sweet-and-sour", "sweetened", "syrupy", "tangy", "tantalizing", "tart", "tasteless", "tasty", "tender", "terrific", "toasted", "tough", "treacly", "unflavored", "unsavory", "unseasoned", "vegan", "vegetarian", "vanilla", "velvety", "vinegary", "warm", "whipped", "wonderful", "yucky", "yummy", "zesty", "zingy" };
    private List<string> foodNouns = new List<string>() { "hamburger", "sandwich", "hot dog", "cherry", "apple", "grapes", "orange", "olives", "watermelon", "carrot", "tomato", "peas", "salad", "vegetables", "pancake", "sausage", "eggs", "potato", "cookies", "fries", "candy", "okonomiyaki", "sushi", "tonkatsu", "ramen", "arroz con leche", "flan", "ceviche", "nachos", "guacamole", "fajitas", "quesadillas", "salsa", "chicken and waffles", "red beans and rice", "pulled pork", "cornbread", "peach cobbler", "mashed potatoes", "Derby pie", "burgoo", "Ale-8-One", "fried catfish" };

    int GetTotalTaskCount()
    {
        return openTasks.Count() + assignedTasksByGUID.Count() + completedTasks.Count();
    }

    void GenerateTask()
    {
        Debug.Log("Generating task...");

        int taskId = GetTotalTaskCount();

        Task newTask = GetRandomTask(taskId);
        openTasks.Add(newTask);

        UpdateTaskCountText();

        mm.ShowTaskOnNode(newTask.destinationNode, newTask);

        Debug.Log("Generated delivery task for " + newTask.name);
    }

    Task GetRandomTask(int taskIdx)
    {
        Task newTask;

        // For now, only task type is delivery
        newTask.taskType = TaskType.DELIVERY;

        // Get random item for delivery
        newTask.name = GetRandomDeliveryItemName();

        // Task is initially unassigned
        newTask.assigneeAgentPreferredName = "";

        // Assign to random untasked node
        int untaskedNodeGraphIdx = GetRandomUntaskedNodeGraphIdx();
        newTask.destinationNode = mm.GetNode(untaskedNodeGraphIdx);

        return newTask;
    }

    // Pretty similar to MapManager.GetRandomUnoccupiedNode()
    int GetRandomUntaskedNodeGraphIdx()
    {
        int startIdx = UnityEngine.Random.Range(0, mm.GetNodeCount());

        int curGraphIDx = startIdx;
        int nodesTriedCount = 0;

        List<int> taskedNodeIndices = new List<int>();

        foreach (Task t in openTasks)
        {
            int nodeGraphIdx = mm.globalIdxToGraphIdx[t.destinationNode.globalIdx];
            taskedNodeIndices.Add(nodeGraphIdx);
        }

        while (nodesTriedCount < mm.GetNodeCount())
        {
            int curGlobalIdx = mm.GetNode(curGraphIDx).globalIdx;

            if (!taskedNodeIndices.Contains(curGraphIDx) && mm.hqNodeCoordinateGlobalIdx != curGlobalIdx)
            {
                // Use this index
                return curGraphIDx;
            }
            else
            {
                curGraphIDx++;
                nodesTriedCount++;

                if (curGraphIDx >= mm.GetNodeCount())
                {
                    curGraphIDx = 0;
                }
            }
        }

        // If we reached this point, then all of the nodes were occupied, so at this point,
        // we'll just allow any node (this is very unlikely...)
        return startIdx;
    }

    string GetRandomDeliveryItemName()
    {
        //string randomFoodAdjective = foodAdjectives[UnityEngine.Random.Range(0, foodAdjectives.Count)];
        string randomFoodNoun = foodNouns[UnityEngine.Random.Range(0, foodNouns.Count)];

        //return randomFoodAdjective + " " + randomFoodNoun;
        return randomFoodNoun;
    }

    public void UpdateTaskCountText()
    {
        openTasksText.text = openTasks.Count() == 1 ? "Open Task: " + openTasks.Count().ToString() : "Open Tasks: " + openTasks.Count().ToString();
        assignedTasksText.text = assignedTasksByGUID.Count() == 1 ? "Assigned Task: " + assignedTasksByGUID.Count().ToString() : "Assigned Tasks: " + assignedTasksByGUID.Count().ToString();
        completedTasksText.text = completedTasks.Count() == 1 ? "Completed Task: " + completedTasks.Count().ToString() : "Completed Tasks: " + completedTasks.Count().ToString();
    }

    IEnumerator GenerateTasks()
    {
        while (true)
        {

            yield return new WaitForSecondsRealtime(timeBetweenTaskGenerationSeconds);

            if (generateTasks)
            {
                GenerateTask();
            }
        }
    }

    public void HandleTaskCompletionRequest()
    {
        // Get agent
        AgentData ad;
        bool gotAgentSuccessfully = am.GetAgentByName(requestedPreferredNameForTaskCompletion, out ad);

        if (!gotAgentSuccessfully)
        {
            taskCompletionSuccessful = false;
            return;
        }

        Task agentTask = assignedTasksByGUID[ad.currentTaskGUID];

        // Fail if agent is too far from destination node
        if (am.GetAgentDistanceNeglectingVerticalHeight(ad, agentTask.destinationNode.pos) > maxDistFromGoalForRecognition)
        {
            taskCompletionSuccessful = false;
            return;
        }

        // Fail if agent has no tasks
        if (ad.currentTaskGUID == "")
        {
            taskCompletionSuccessful = false;
            return;
        }

        CompleteTask(ad.currentTaskGUID, ad.preferredName);
    }

    public void HandleTaskAssignmentRequest()
    {
        int agentIdx;

        // Look for agent with this name
        bool agentIdxFound = am.GetAgentIdxByName(requestedPreferredNameForTaskAssignment, out agentIdx);

        if (!agentIdxFound)
        {
            Debug.LogError("failed to get agent " + requestedPreferredNameForTaskAssignment);

            taskAssignmentSuccessful = false;
            return;
        }

        AgentData ad = am.agents[agentIdx];

        // Fail if agent is too far from HQ
        if (am.GetAgentDistanceNeglectingVerticalHeight(ad, mm.hqNodePos) > maxDistFromGoalForRecognition)
        {
            Debug.LogError("Failed because agent too far: " + am.GetAgentDistanceNeglectingVerticalHeight(ad, mm.hqNodePos));

            Debug.Log("Agent position: " + ad.latestPosition);
            Debug.Log("HQ position: " + mm.hqNodePos);

            taskAssignmentSuccessful = false;
            return;
        }

        // Fail if agent already has a task
        if (ad.currentTaskGUID != "")
        {
            Debug.LogError("Failed because agent " + ad.preferredName +" already has a task");

            taskAssignmentSuccessful = false;
            return;
        }

        // Check for open tasks
        if (openTasks.Count() == 0)
        {
            Debug.LogError("Failed because no open tasks");

            taskAssignmentSuccessful = false;
            return;
        }

        // Sort tasks by distance
        openTasks = openTasks.OrderBy(t => Vector3.Distance(t.destinationNode.pos, mm.hqNodePos)).ToList();

        // Give most appropriate task for agent type
        int taskIdxToAssign;

        if (ad.agentType == AgentType.FAST_LOAD_SLOW_MOVE)
        {
            // Assign closest task
            taskIdxToAssign = 0;
        }
        else
        {
            // Assign farthest task
            taskIdxToAssign = openTasks.Count-1;
        }

        AssignTask(taskIdxToAssign, agentIdx);

        taskAssignmentSuccessful = true;
    }

    private void AssignTask(int oldTaskIdx, int agentIdx)
    {
        AgentData agentToAssign = am.agents[agentIdx];
        Task taskToAssign = openTasks[oldTaskIdx];

        taskToAssign.assigneeAgentPreferredName = am.agents[agentIdx].preferredName;

        // Re-number task idx for agent and task itself
        string newTaskGUID = Guid.NewGuid().ToString();
        agentToAssign.currentTaskGUID = newTaskGUID;

        Debug.Log("Assigning new task to agent " + agentToAssign.preferredName + ": " + newTaskGUID);

        // Move task from open to assigned list and update agent
        openTasks.RemoveAt(oldTaskIdx);
        assignedTasksByGUID.Add(newTaskGUID, taskToAssign);
        am.agents[agentIdx] = agentToAssign;

        mostRecentAssignedTaskGuid = newTaskGUID;

        UpdateTaskCountText();
    }

    private void CompleteTask(string taskGUID, string agentPreferredName)
    {
        int agentIdx;

        // Look for agent with this name
        bool agentIdxFound = am.GetAgentIdxByName(agentPreferredName, out agentIdx);

        if (!agentIdxFound)
        {
            Debug.LogError("Unable to find agent by name " + agentPreferredName + " so abandoning task completion.");
        }

        AgentData ad = am.agents[agentIdx];

        Task taskToComplete = assignedTasksByGUID[taskGUID];

        ad.currentTaskGUID = "";

        am.agents[agentIdx] = ad;

        // Move task from assigned list to completed list
        assignedTasksByGUID.Remove(taskGUID);
        completedTasks.Add(taskToComplete);

        // Remove UI message showing task
        mm.ClearTaskOnNode(taskToComplete.destinationNode);
        UpdateTaskCountText();
    }

    public void PublicStart()
    {
        UpdateTaskCountText();

        taskAssignmentSuccessful = false;
        taskCompletionSuccessful = false;
        taskAssignmentRequestIssued = false;
        taskCompletionRequestIssued = false;

        StartCoroutine(GenerateTasks());
    }

    void Update()
    {
        if (taskAssignmentRequestIssued)
        {
            HandleTaskAssignmentRequest();
            taskAssignmentRequestIssued = false;
        }

        if (taskCompletionRequestIssued)
        {
            HandleTaskCompletionRequest();
            taskCompletionRequestIssued = false;
        }
    }

    public void RequestTaskAssignment(string preferredName)
    {
        taskAssignmentRequestIssued = true;
        requestedPreferredNameForTaskAssignment = preferredName;
    }

    public void RequestTaskCompletion(string preferredName)
    {
        taskCompletionRequestIssued = true;
        requestedPreferredNameForTaskCompletion = preferredName;
    }
}
