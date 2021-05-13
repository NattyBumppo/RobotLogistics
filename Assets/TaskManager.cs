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
    public int assigneeAgentIdx;
    public GraphNode destinationNode;
    public int idxInTaskList;
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
    public int mostRecentAssignedTaskIdx;
    public bool taskAssignmentRequestIssued;
    public bool taskAssignmentSuccessful;

    public string requestedPreferredNameForTaskCompletion;
    public bool taskCompletionRequestIssued;
    public bool taskCompletionSuccessful;

    public List<Task> openTasks = new List<Task>();
    public List<Task> assignedTasks = new List<Task>();
    public List<Task> completedTasks = new List<Task>();

    private List<string> foodAdjectives = new List<string>() { "acidic", "appealing", "appetizing", "aromatic", "astringent", "aromatic", "baked", "balsamic", "beautiful", "bite-size", "bitter", "bland", "blended", "boiled", "briny", "brown", "burnt", "buttered", "caked", "candied", "caramelized", "cheesy", "chocolate", "cholesterol-free", "classic", "classy", "cold", "cool", "crafted", "creamed", "creamy", "crisp", "crunchy", "cured", "dazzling", "deep-fried", "delectable", "delicious", "distinctive", "doughy", "drizzle", "dried", "extraordinary", "famous", "fantastic", "fizzy", "flaky", "flavored", "flavorful", "fluffy", "fresh", "fried", "frozen", "fruity", "garlic", "generous", "gingery", "glazed", "golden", "gorgeous", "gourmet", "greasy", "grilled", "gritty", "halal", "honey", "hot", "icy", "infused", "insipid", "intense", "juicy", "jumbo", "kosher", "large", "lavish", "lean", "low-fat", "luscious", "marinated", "mashed", "mellow", "mild", "minty", "moist", "mouth-watering", "natural", "non-fat", "nutty", "oily", "organic", "overpowering", "peppery", "petite", "pickled", "piquant", "plain", "pleasant", "plump", "poached", "prickly", "pulpy", "pungent", "pureed", "rich", "roasted", "robust", "rotten", "rubbery", "saccharine", "salty", "savory", "sapid", "saporous", "sauteed", "savory", "scrumptious", "seared", "seasoned", "silky", "simmered", "sizzling", "smelly", "smoked", "smoky", "smothered", "sour", "southern-style", "special", "spiced", "spicy", "spiral-cut", "spongy", "stale", "steamed", "sticky", "strawberry-flavored", "stuffed", "succulent", "sugar-coated", "sugar-free", "sugared", "sugarless", "sugary", "superb", "sweet", "sweet-and-sour", "sweetened", "syrupy", "tangy", "tantalizing", "tart", "tasteless", "tasty", "tender", "terrific", "toasted", "tough", "treacly", "unflavored", "unsavory", "unseasoned", "vegan", "vegetarian", "vanilla", "velvety", "vinegary", "warm", "whipped", "wonderful", "yucky", "yummy", "zesty", "zingy" };
    private List<string> foodNouns = new List<string>() { "burger", "sandwich", "hot dog", "cherry", "apple", "grapes", "orange", "olives", "watermelon", "carrot", "tomato", "peas", "salad", "vegetables", "pancake", "sausage", "eggs", "potato", "cookies", "fries", "candy", "okonomiyaki", "sushi", "tonkatsu", "ramen" };

    int GetTotalTaskCount()
    {
        return openTasks.Count() + assignedTasks.Count() + completedTasks.Count();
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
        newTask.assigneeAgentIdx = -1;

        // Assign to random untasked node
        int untaskedNodeGraphIdx = GetRandomUntaskedNodeGraphIdx();
        newTask.destinationNode = mm.GetNode(untaskedNodeGraphIdx);

        // Record index
        newTask.idxInTaskList = taskIdx;

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
        string randomFoodAdjective = foodAdjectives[UnityEngine.Random.Range(0, foodAdjectives.Count)];
        string randomFoodNoun = foodNouns[UnityEngine.Random.Range(0, foodNouns.Count)];

        return randomFoodAdjective + " " + randomFoodNoun;
    }

    public void UpdateTaskCountText()
    {
        openTasksText.text = openTasks.Count() == 1 ? "Open Task: " + openTasks.Count().ToString() : "Open Tasks: " + openTasks.Count().ToString();
        assignedTasksText.text = assignedTasks.Count() == 1 ? "Assigned Task: " + assignedTasks.Count().ToString() : "Assigned Tasks: " + assignedTasks.Count().ToString();
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

        Task agentTask = assignedTasks[ad.currentTaskIdx];

        // Fail if agent is too far from destination node
        if (am.GetAgentDistanceNeglectingVerticalHeight(ad, agentTask.destinationNode.pos) > maxDistFromGoalForRecognition)
        {
            taskCompletionSuccessful = false;
            return;
        }

        // Fail if agent has no tasks
        if (ad.currentTaskIdx == -1)
        {
            taskCompletionSuccessful = false;
            return;
        }

        CompleteTask(ad.currentTaskIdx, ad.idxInAgentsList);
    }

    public void HandleTaskAssignmentRequest()
    {
        // Get agent
        AgentData ad;
        bool gotAgentSuccessfully = am.GetAgentByName(requestedPreferredNameForTaskAssignment, out ad);
        
        if (!gotAgentSuccessfully)
        {
            Debug.LogError("failed to get agent " + requestedPreferredNameForTaskAssignment);

            taskAssignmentSuccessful = false;
            return;
        }

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
        if (ad.currentTaskIdx != -1)
        {
            Debug.LogError("Failed because agent already has a task");

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

        AssignTask(taskIdxToAssign, ad.idxInAgentsList);

        taskAssignmentSuccessful = true;
    }

    private void AssignTask(int oldTaskIdx, int agentIdx)
    {
        AgentData agentToAssign = am.agents[agentIdx];
        Task taskToAssign = openTasks[oldTaskIdx];

        agentToAssign.currentTaskIdx = taskToAssign.idxInTaskList;
        taskToAssign.assigneeAgentIdx = agentToAssign.idxInAgentsList;

        // Re-number task idx for agent and task itself
        int newTaskIdx = assignedTasks.Count;
        agentToAssign.currentTaskIdx = newTaskIdx;
        taskToAssign.idxInTaskList = newTaskIdx;

        // Move task from open to assigned list and update agent
        openTasks.RemoveAt(oldTaskIdx);
        assignedTasks.Add(taskToAssign);
        am.agents[agentIdx] = agentToAssign;

        mostRecentAssignedTaskIdx = newTaskIdx;
    }

    private void CompleteTask(int taskIdx, int agentIdx)
    {
        AgentData agentCompletingTask = am.agents[agentIdx];
        Task taskToComplete = assignedTasks[taskIdx];

        agentCompletingTask.currentTaskIdx = -1;

        am.agents[agentCompletingTask.idxInAgentsList] = agentCompletingTask;

        // Move task from assigned list to completed list
        assignedTasks.RemoveAt(taskIdx);
        completedTasks.Add(taskToComplete);

        // Remove UI message showing task
        mm.ClearTaskOnNode(taskToComplete.destinationNode);        
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
