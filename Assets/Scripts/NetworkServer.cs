using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AgentRequestType
{
    REGISTRATION,
    REQUEST_FOR_WORK,
    POSITION_UPDATE,
    TASK_COMPLETE,
    REQUEST_FOR_CAMERA_DATA
}

public enum WorkRequestResponseType
{
    SUCCESS,
    FAILURE_AGENT_TOO_FAR,
    FAILURE_NO_TASKS,
    FAILURE_OTHER
}

public class NetworkServer : MonoBehaviour
{

// Types of messages (requests from agents to server)
//-Registration (response: ack with map of city)
//-Request for work (response: task information or no task (along with succeed code, including "you're too far away" and "no tasks available"))
//-Position update (response: ack)
//-Task complete (response: ack)

//Request layout
//===
//First byte: request type

//For registration:

    void HandleRequestFromAgent(byte[] request)
    {
        // First read type code


        // Next, parse payload (if applicable)


        // Do any necessary data processing or registration


        // Make a response


        // Send response
    }

    byte[] MakeResponseToRegistration()
    {
        byte[] response = new byte[255];

        // Ack sends back the ID of the sender and the full graph in plaintext text

        return response;
    }



    byte[] MakeResponseToWorkRequest()
    {
        byte[] response = new byte[255];


        // Ack sends back the ID of the sender and the succeed code

        return response;
    }

    byte[] MakeResponseToPositionUpdate()
    {
        byte[] response = new byte[255];


        // Ack sends back the ID of the sender and the logged position

        return response;
    }

    byte[] MakeResponseToTaskComplete()
    {
        byte[] response = new byte[255];


        return response;
    }


    byte[] MakeResponseToRequestForCameraData()
    {
        byte[] response = new byte[255];


        return response;
    }

    void Start()
    {
        
    }


    void Update()
    {
        
    }
}
