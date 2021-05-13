using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

public enum AgentRequestType
{
    REGISTRATION,
    REQUEST_FOR_TASK,
    POSITION_UPDATE,
    TASK_COMPLETE,
    DEREGISTRATION,
    STATUS_UPDATE
}

public enum WorkRequestResponseType
{
    SUCCESS,
    FAILURE_NO_TASKS,
    FAILURE_REQUEST_PARSING_ERROR,
    FAILURE_OTHER
}

public class NetworkServer : MonoBehaviour
{
    public AgentManager am;
    public MapManager mm;
    public TaskManager tm;

    public System.Threading.Thread SocketThread;
    TcpListener server = null;

    public Text ipAndPortText;

    public string ipTextToSet;
    public string portTextToSet;
    public bool ipPortTextNeedsToBeSet = false;

    private int DATA_REQUEST_PACKET_LENGTH = 64;

    private byte[] CombineByteArrays(params byte[][] arrays)
    {
        byte[] rv = new byte[arrays.Sum(a => a.Length)];
        int offset = 0;
        foreach (byte[] array in arrays)
        {
            System.Buffer.BlockCopy(array, 0, rv, offset, array.Length);
            offset += array.Length;
        }
        return rv;
    }

    public void PublicStart()
    {
        Application.runInBackground = true;
        StartServer();
    }

    void Update()
    {
        if (ipPortTextNeedsToBeSet)
        {
            ipAndPortText.text = ipTextToSet + "\t\t " + portTextToSet;

            ipPortTextNeedsToBeSet = false;
        }
    }

    public void StartServer()
    {
        // Open up a socket to listen for requests
        SocketThread = new System.Threading.Thread(ProcessNetworkRequests);
        SocketThread.IsBackground = true;
        SocketThread.Start();
    }

    private IPAddress GetGlobalIPAddress()
    {
        IPAddress localIP = IPAddress.Parse("127.0.0.1");

        try
        {
            foreach (NetworkInterface netInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (netInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                    netInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                {
                    foreach (UnicastIPAddressInformation addrInfo in netInterface.GetIPProperties().UnicastAddresses)
                    {
                        if (addrInfo.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            localIP = addrInfo.Address;
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            // Exception occurred!
            Debug.LogError("Error occurred while trying to get global IP address:");
            Debug.LogError(e.ToString());
        }

        return localIP;
    }

    public int GetFreeTcpPort()
    {
        TcpListener l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    void ProcessNetworkRequests()
    {
        try
        {
            // Set the TcpListener to a free port
            IPAddress globalAddress = GetGlobalIPAddress();
            Int32 port = GetFreeTcpPort();

            ipTextToSet = "IP: " + globalAddress.ToString();
            portTextToSet = "Port: " + port.ToString();
            ipPortTextNeedsToBeSet = true;

            server = new TcpListener(globalAddress, port);

            // Start listening for client requests.
            server.Start();

            // Buffer for reading data
            Byte[] bytes = new Byte[DATA_REQUEST_PACKET_LENGTH];

            // Enter the listening loop.
            while (true)
            {
                //Debug.Log("Waiting for a connection... ");

                // Perform a blocking call to accept requests.
                // You could also use server.AcceptSocket() here.
                TcpClient client = server.AcceptTcpClient();
                //Debug.Log("Connected!");

                // Get a stream object for reading and writing
                NetworkStream stream = client.GetStream();

                // Loop to receive all the data sent by the client.
                stream.Read(bytes, 0, bytes.Length);
                //Debug.Log("Received request (" + bytes.Length + "B) from client.");

                byte[] response = HandleRequestFromAgent(bytes, client);

                //Debug.Log("Parse success? " + (response.Length > 0));

                stream.Write(response, 0, response.Length);
                //Debug.Log("Sent response (" + response.Length + "B)");

                // Shutdown and end connection
                client.Close();
            }
        }
        catch (SocketException e)
        {
            Debug.Log("SocketException: " + e);
        }
        finally
        {
            // Stop listening for new clients.
            server.Stop();
        }

        Debug.Log("\nHit enter to continue...");
        Console.Read();
    }
    
    //byte[] MakeDataResponsePacketBytes(WorkRequestResponseType responseType, UInt32 responseID, byte[] sensorDataPayload)
    //{
    //    // Prepare struct
    //    DataResponsePacket drp = new DataResponsePacket();
    //    drp.packetLength = sensorDataPayload.Length + DATA_RESPONSE_HEADER_LENGTH_BYTES;
    //    drp.responseType = responseType;
    //    drp.responseID = responseID;
    //    drp.payloadSize = (UInt32)sensorDataPayload.Length;
    //    drp.sensorDataPayload = sensorDataPayload;

    //    // Convert struct to bytes
    //    byte[] packetLengthBytes = BitConverter.GetBytes(drp.packetLength);
    //    byte[] statusBytes = new byte[1];
    //    statusBytes[0] = (byte)drp.responseType;
    //    byte[] responseIDBytes = BitConverter.GetBytes(drp.responseID);
    //    byte[] payloadSizeBytes = BitConverter.GetBytes(drp.payloadSize);

    //    // Reverse all data endianness if it's not in network byte order (big endian)
    //    if (BitConverter.IsLittleEndian)
    //    {
    //        Array.Reverse(packetLengthBytes);
    //        Array.Reverse(responseIDBytes);
    //        Array.Reverse(payloadSizeBytes);
    //    }

    //    return CombineByteArrays(packetLengthBytes, statusBytes, responseIDBytes, payloadSizeBytes, drp.sensorDataPayload);
    //}

    float SwapEndian(float input)
    {
        byte[] bytes = BitConverter.GetBytes(input);
        Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    UInt32 SwapEndian(UInt32 input)
    {
        byte[] bytes = BitConverter.GetBytes(input);
        Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    private bool BytesToClientMessage(byte[] bytes)
    {
        //drp = new DataRequestPacket();

        //// Make sure packet is the expected size
        //if (bytes.Length != DATA_REQUEST_PACKET_LENGTH_BYTES)
        //{
        //    return false;
        //}

        //// Parse packet
        //int size = Marshal.SizeOf(drp);
        //IntPtr ptr = Marshal.AllocHGlobal(size);

        //Marshal.Copy(bytes, 0, ptr, size);

        //drp = (DataRequestPacket)Marshal.PtrToStructure(ptr, drp.GetType());
        //Marshal.FreeHGlobal(ptr);

        //// Fix endianness if necessary
        //if (BitConverter.IsLittleEndian)
        //{
        //    drp.responseID = SwapEndian(drp.responseID);
        //}

        return true;
    }

    void StopServer()
    {
        // Stop thread
        if (SocketThread != null)
        {
            SocketThread.Abort();
        }

        if (server != null)
        {
            server.Stop();
            Debug.Log("Disconnected!");
        }
    }

    void OnDisable()
    {
        StopServer();
    }
    
    byte[] HandleRegistrationRequest(byte[] bytes, string hostname, int port)
    {
        Debug.Log("RegistrationRequest");

        // Check length (should be DATA_REQUEST_PACKET_LENGTH bytes; return error otherwise)
        if (bytes.Length != DATA_REQUEST_PACKET_LENGTH)
        {
            Debug.LogError("Error: bad byte length of " + bytes.Length);
            return ConstructResponse(WorkRequestResponseType.FAILURE_REQUEST_PARSING_ERROR);
        }

        // Parse color and preferred name
        byte[] rBytes = bytes.Skip(1).Take(4).ToArray();
        byte[] gBytes = bytes.Skip(5).Take(4).ToArray();
        byte[] bBytes = bytes.Skip(9).Take(4).ToArray();
        byte[] preferredNameBytes = bytes.Skip(13).Take(16).ToArray();

        // Reverse all data endianness if it's not in network byte order (big endian)
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(rBytes);
            Array.Reverse(gBytes);
            Array.Reverse(bBytes);
            // preferredNameBytes string is passed as individual chars so endianness isn't a factor
        }

        float r = BitConverter.ToSingle(rBytes, 0);
        float g = BitConverter.ToSingle(gBytes, 0);
        float b = BitConverter.ToSingle(bBytes, 0);

        Color color = new Color(r, g, b);

        // Parse preferred name
        string preferredName = System.Text.Encoding.ASCII.GetString(preferredNameBytes).Trim();

        // Issue request to register new agent and wait
        am.RequestAgentCreation(color, hostname, port, preferredName);

        while (am.agentCreationRequestIssued)
        {
            // Wait for request to be processed
            Thread.Sleep(10);
        }

        // Attach the graph index of the agent to the payload
        AgentData ad;
        bool agentFound = am.GetAgentByName(preferredName, out ad);

        if (!agentFound)
        {
            Debug.LogError("Error: couldn't find agent named " + preferredName + " after creation!");
            ConstructResponse(WorkRequestResponseType.FAILURE_OTHER);
        }

        byte[] graphIdxBytes = BitConverter.GetBytes(ad.lastNodeIdxVisited);

        // Reverse all data endianness if it's not in network byte order (big endian)
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(graphIdxBytes);
        }

        // Grab map as a string and attach as payload to response
        string mapString = mm.GraphToString();

        return ConstructResponse(WorkRequestResponseType.SUCCESS, CombineByteArrays(graphIdxBytes, Encoding.ASCII.GetBytes(mapString)));
    }

    byte[] HandleWorkRequest(byte[] bytes)
    {
        Debug.Log("WorkRequest");

        // Check length (should be DATA_REQUEST_PACKET_LENGTH bytes; return error otherwise)
        if (bytes.Length != DATA_REQUEST_PACKET_LENGTH)
        {
            Debug.LogError("Error: bad byte length of " + bytes.Length);
            return ConstructResponse(WorkRequestResponseType.FAILURE_REQUEST_PARSING_ERROR);
        }

        // Parse preferred name
        byte[] preferredNameBytes = bytes.Skip(1).Take(16).ToArray();
        string preferredName = System.Text.Encoding.ASCII.GetString(preferredNameBytes).Trim();

        Debug.Log("Parsed preferred name of " + preferredName);

        // Fail immediately if no tasks are available
        if (tm.openTasks.Count() == 0)
        {
            Debug.Log("Sending failure response to work request due to not enough tasks.");

            return ConstructResponse(WorkRequestResponseType.FAILURE_NO_TASKS);
        }
        else
        {
            // Issue request to attempt task assignment and wait
            tm.RequestTaskAssignment(preferredName);

            while (tm.taskAssignmentRequestIssued)
            {
                // Wait for request to be proceed
                Thread.Sleep(10);
            }

            // Check if assignment was successful and respond accordingly
            if (tm.taskAssignmentSuccessful)
            {
                // We'll send back the name of the task and its location

                // Limit task name string to 32 bytes, just in case it's too long
                Task assignedTask = tm.assignedTasksByGUID[tm.mostRecentAssignedTaskGuid];
                int substringLength = Math.Min(32, assignedTask.name.Length);

                string shortenedTaskName = assignedTask.name.Substring(0, substringLength).PadRight(32);
                byte[] taskNameBytes = Encoding.ASCII.GetBytes(shortenedTaskName);
                byte[] taskGraphIdxBytes = BitConverter.GetBytes((uint)mm.globalIdxToGraphIdx[assignedTask.destinationNode.globalIdx]);

                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(taskGraphIdxBytes);
                }

                Debug.Log("Sending successful response to work request with " + taskNameBytes.Length + " bytes for name and " + taskGraphIdxBytes.Length + " bytes for graph index");

                return ConstructResponse(WorkRequestResponseType.SUCCESS, CombineByteArrays(taskNameBytes, taskGraphIdxBytes));
            }
            else
            {
                Debug.Log("Sending failure response to work request due to parsing error");

                return ConstructResponse(WorkRequestResponseType.FAILURE_REQUEST_PARSING_ERROR);
            }
        }
    }

    byte[] HandlePositionUpdate(byte[] bytes)
    {
        Debug.Log("PositionUpdate");

        // Check length (should be DATA_REQUEST_PACKET_LENGTH bytes; return error otherwise)
        if (bytes.Length != DATA_REQUEST_PACKET_LENGTH)
        {
            Debug.LogError("Error: bad byte length of " + bytes.Length);
            return ConstructResponse(WorkRequestResponseType.FAILURE_REQUEST_PARSING_ERROR);
        }

        // Grab position data
        byte[] startNodeGraphIdxBytes = bytes.Skip(1).Take(4).ToArray();
        byte[] endNodeGraphIdxBytes = bytes.Skip(5).Take(4).ToArray();
        byte[] fractionBytes = bytes.Skip(9).Take(4).ToArray();
        byte[] preferredNameBytes = bytes.Skip(13).Take(16).ToArray();

        // Reverse all data endianness if it's not in network byte order (big endian)
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(startNodeGraphIdxBytes);
            Array.Reverse(endNodeGraphIdxBytes);
            Array.Reverse(fractionBytes);
            // preferredNameBytes string is passed as individual chars so endianness isn't a factor
        }

        // Convert data
        int startNodeGraphIdx = (int)BitConverter.ToUInt32(startNodeGraphIdxBytes, 0);
        int endNodeGraphIdx = (int)BitConverter.ToUInt32(endNodeGraphIdxBytes, 0);
        float fraction = BitConverter.ToSingle(fractionBytes, 0);
        string preferredName = System.Text.Encoding.ASCII.GetString(preferredNameBytes).Trim();

        // Move agent
        am.RequestAgentPositionUpdate(startNodeGraphIdx, endNodeGraphIdx, fraction, preferredName);

        while (am.agentPositionUpdateRequestIssued)
        {
            // Wait for request to be procseed
            Thread.Sleep(10);
        }

        return ConstructResponse(WorkRequestResponseType.SUCCESS);
    }

    byte[] HandleTaskComplete(byte[] bytes)
    {
        Debug.Log("TaskComplete");

        // Check length (should be DATA_REQUEST_PACKET_LENGTH bytes; return error otherwise)
        if (bytes.Length != DATA_REQUEST_PACKET_LENGTH)
        {
            Debug.LogError("Error: bad byte length of " + bytes.Length);
            return ConstructResponse(WorkRequestResponseType.FAILURE_REQUEST_PARSING_ERROR);
        }

        // Parse preferred name
        byte[] preferredNameBytes = bytes.Skip(1).Take(16).ToArray();
        string preferredName = System.Text.Encoding.ASCII.GetString(preferredNameBytes).Trim();

        Debug.Log("Parsed preferred name of " + preferredName);

        // Issue request to to complete task and wait
        tm.RequestTaskCompletion(preferredName);

        while (tm.taskCompletionRequestIssued)
        {
            // Wait for request to be procseed
            Thread.Sleep(10);
        }

        return ConstructResponse(WorkRequestResponseType.SUCCESS);
    }

    byte[] HandleDeregistrationRequest(byte[] bytes)
    {
        Debug.Log("DeregistrationRequest");

        // Check length (should be DATA_REQUEST_PACKET_LENGTH bytes; return error otherwise)
        if (bytes.Length != DATA_REQUEST_PACKET_LENGTH)
        {
            Debug.LogError("Error: bad byte length of " + bytes.Length);
            return ConstructResponse(WorkRequestResponseType.FAILURE_REQUEST_PARSING_ERROR);
        }

        // Parse preferred name
        byte[] preferredNameBytes = bytes.Skip(1).Take(16).ToArray();
        string preferredName = System.Text.Encoding.ASCII.GetString(preferredNameBytes).Trim();

        Debug.Log("Parsed preferred name of " + preferredName);

        // Issue request to deregister new agent and wait
        am.RequestAgentDestruction(preferredName);

        while (am.agentDestructionRequestIssued)
        {
            // Wait for request to be procseed
            Thread.Sleep(10);
        }

        return ConstructResponse(WorkRequestResponseType.SUCCESS);
    }

    byte[] HandleStatusUpdateRequest(byte[] bytes)
    {
        Debug.Log("StatusUpdateRequest");

        // Check length (should be DATA_REQUEST_PACKET_LENGTH bytes; return error otherwise)
        if (bytes.Length != DATA_REQUEST_PACKET_LENGTH)
        {
            Debug.LogError("Error: bad byte length of " + bytes.Length);
            return ConstructResponse(WorkRequestResponseType.FAILURE_REQUEST_PARSING_ERROR);
        }

        byte[] agentStatusBytes = bytes.Skip(1).Take(31).ToArray();
        byte[] preferredNameBytes = bytes.Skip(32).Take(16).ToArray();

        // Convert data
        string statusMessage = System.Text.Encoding.ASCII.GetString(agentStatusBytes).Trim();
        string preferredName = System.Text.Encoding.ASCII.GetString(preferredNameBytes).Trim();

        Debug.Log("Got status message of '" + statusMessage + "' for agent " + preferredName);

        // Issue request to deregister new agent and wait
        am.RequestAgentStatusUpdate(statusMessage, preferredName);

        while (am.agentStatusUpdateRequestIssued)
        {
            // Wait for request to be procseed
            Thread.Sleep(10);
        }

        return ConstructResponse(WorkRequestResponseType.SUCCESS);
    }

    byte[] ConstructResponse(WorkRequestResponseType responseType /* no payload */)
    {
        // Length is response type one byte
        int packetLength = 1;

        byte[] packetLengthBytes = BitConverter.GetBytes(packetLength);
        // Reverse all data endianness if it's not in network byte order (big endian)
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(packetLengthBytes);
        }

        byte[] responseTypeBytes = new byte[1] { (byte)responseType };

        return CombineByteArrays(packetLengthBytes, responseTypeBytes);
    }

    byte[] ConstructResponse(WorkRequestResponseType responseType, byte[] payload)
    {
        // Length is one response type byte + payload
        int packetLength = 1 + payload.Length;
        byte[] packetLengthBytes = BitConverter.GetBytes(packetLength);

        // Reverse all data endianness if it's not in network byte order (big endian)
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(packetLengthBytes);
        }

        byte[] responseTypeBytes = new byte[1] { (byte)responseType };

        return CombineByteArrays(packetLengthBytes, responseTypeBytes, payload);
    }

    byte[] HandleRequestFromAgent(byte[] request, TcpClient clientInfo)
    {
        // Parse client data
        string hostname = ((IPEndPoint)clientInfo.Client.RemoteEndPoint).Address.ToString();
        int port = ((IPEndPoint)clientInfo.Client.RemoteEndPoint).Port;

        // Next, read type code
        AgentRequestType requestCode = (AgentRequestType)request[0];

        switch(requestCode)
        {
            case AgentRequestType.REGISTRATION:
                return HandleRegistrationRequest(request, hostname, port);
            case AgentRequestType.REQUEST_FOR_TASK:
                return HandleWorkRequest(request);
            case AgentRequestType.POSITION_UPDATE:
                return HandlePositionUpdate(request);
            case AgentRequestType.TASK_COMPLETE:
                return HandleTaskComplete(request);
            case AgentRequestType.DEREGISTRATION:
                return HandleDeregistrationRequest(request);
            case AgentRequestType.STATUS_UPDATE:
                return HandleStatusUpdateRequest(request);
            default:
                // Unrecognized request code; send back an error
                return ConstructResponse(WorkRequestResponseType.FAILURE_REQUEST_PARSING_ERROR);
        }
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
}
