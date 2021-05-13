using System.Collections;
using System.Collections.Generic;
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
}

public class TaskManager : MonoBehaviour
{
    public MapManager mm;

    public float timeBetweenTaskGenerationSeconds;
    public bool generateTasks;

    public Text openTaskCountText;
    public int openTaskCount;
    public Text assignedTaskCountText;
    public int assignedTaskCount;

    public List<Task> tasks = new List<Task>();

    private List<string> foodAdjectives = new List<string>() { "acidic", "appealing", "appetizing", "aromatic", "astringent", "aromatic", "baked", "balsamic", "beautiful", "bite-size", "bitter", "bland", "blended", "boiled", "briny", "brown", "burnt", "buttered", "caked", "candied", "caramelized", "cheesy", "chocolate", "cholesterol-free", "classic", "classy", "cold", "cool", "crafted", "creamed", "creamy", "crisp", "crunchy", "cured", "dazzling", "deep-fried", "delectable", "delicious", "distinctive", "doughy", "drizzle", "dried", "extraordinary", "famous", "fantastic", "filet", "fizzy", "flaky", "flavored", "flavorful", "fluffy", "fresh", "fried", "frozen", "fruity", "garlic", "generous", "gingery", "glazed", "golden", "gorgeous", "gourmet", "greasy", "grilled", "gritty", "halal", "honey", "hot", "icy", "infused", "insipid", "intense", "juicy", "jumbo", "kosher", "large", "lavish", "lean", "low-fat", "luscious", "marinated", "mashed", "mellow", "mild", "minty", "moist", "mouth-watering", "natural", "non-fat", "nutty", "oily", "organic", "overpowering", "peppery", "petite", "pickled", "piquant", "plain", "pleasant", "plump", "poached", "prickly", "pulpy", "pungent", "pureed", "rich", "roasted", "robust", "rotten", "rubbery", "saccharine", "salty", "savory", "sapid", "saporous", "sauteed", "savory", "scrumptious", "seared", "seasoned", "silky", "simmered", "sizzling", "smelly", "smoked", "smoky", "smothered", "sour", "southern-style", "special", "spiced", "spicy", "spiral-cut", "spongy", "stale", "steamed", "sticky", "strawberry-flavored", "stuffed", "succulent", "sugar-coated", "sugar-free", "sugared", "sugarless", "sugary", "superb", "sweet", "sweet-and-sour", "sweetened", "syrupy", "tangy", "tantalizing", "tart", "tasteless", "tasty", "tender", "terrific", "toasted", "tough", "treacly", "unflavored", "unsavory", "unseasoned", "vegan", "vegetarian", "vanilla", "velvety", "vinegary", "warm", "whipped", "wonderful", "yucky", "yummy", "zesty", "zingy" };
    private List<string> foodNouns = new List<string>() { "burger", "sandwich", "hot dog", "cherry", "apple", "grapes", "orange", "olives", "watermelon", "carrot", "tomato", "peas", "salad", "vegetables", "pancake", "sausage", "eggs", "potato", "cookies", "fries", "candy", "okonomiyaki", "sushi", "tonkatsu", "ramen" };

    void GenerateTask()
    {
        Debug.Log("Generating task...");

        Task newTask = GetRandomTask();
        tasks.Add(newTask);

        openTaskCount++;

        UpdateTaskCountText();

        mm.ShowTaskOnNode(newTask.destinationNode);

        Debug.Log("Generated delivery task for " + newTask.name);
    }

    Task GetRandomTask()
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

        return newTask;
    }

    // Pretty similar to MapManager.GetRandomUnoccupiedNode()
    int GetRandomUntaskedNodeGraphIdx()
    {
        int startIdx = UnityEngine.Random.Range(0, mm.GetNodeCount());

        int curGraphIDx = startIdx;
        int nodesTriedCount = 0;

        List<int> taskedNodeIndices = new List<int>();

        foreach (Task t in tasks)
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
        string randomFoodAdjective = foodAdjectives[Random.Range(0, foodAdjectives.Count)];
        string randomFoodNoun = foodNouns[Random.Range(0, foodNouns.Count)];

        return randomFoodAdjective + " " + randomFoodNoun;
    }

    void UpdateTaskCountText()
    {
        openTaskCountText.text = openTaskCount == 1 ? "Open Task: " + openTaskCount.ToString() : "Open Tasks: " + openTaskCount.ToString();
        assignedTaskCountText.text = assignedTaskCount == 1 ? "Assigned Task: " + assignedTaskCount.ToString() : "Assigned Tasks: " + assignedTaskCount.ToString();
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

    
    public void PublicStart()
    {
        openTaskCount = 0;
        assignedTaskCount = 0;
        UpdateTaskCountText();

        StartCoroutine(GenerateTasks());
    }

    void Update()
    {
        
    }
}
