using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TaskManager : MonoBehaviour
{
    public float timeBetweenTaskGenerationSeconds;
    public bool generateTasks;

    public Text openTaskCountText;
    public int openTaskCount;
    public Text assignedTaskCountText;
    public int assignedTaskCount;

    void GenerateTask()
    {
        Debug.Log("Generating task...");
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
