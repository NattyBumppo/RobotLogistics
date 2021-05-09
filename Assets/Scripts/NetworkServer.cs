using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

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

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DataRequestPacket
{
    public UInt32 responseID;
}

public struct DataResponsePacket
{
    public Int32 packetLength;
    public WorkRequestResponseType responseType;
    public UInt32 responseID;
    public UInt32 payloadSize;
    public byte[] sensorDataPayload;
}

public class NetworkServer : MonoBehaviour
{
    public System.Threading.Thread SocketThread;
    TcpListener server = null;

    public Text ipText;
    public Text portText;

    public string ipTextToSet;
    public string portTextToSet;
    public bool ipPortTextNeedsToBeSet = false;

    private List<byte> captureBytes = new List<byte>();
    private bool captureBytesReady = false;
    private DataRequestPacket captureRequestParameters = new DataRequestPacket();
    private bool captureRequested = false;
    public Camera cameraForNextCapture;

    private int DATA_REQUEST_PACKET_LENGTH_BYTES = 144;
    private int DATA_RESPONSE_HEADER_LENGTH_BYTES = 14;

    public byte[] sensorDataByteBuffer;

    private List<System.Diagnostics.Process> startedProcesses = new List<System.Diagnostics.Process>();

    private void OnApplicationQuit()
    {
        // End processes, just in case
        foreach (System.Diagnostics.Process p in startedProcesses)
        {
            if (p != null)
            {
                p.Kill();
            }
        }

    }

    private IEnumerator CaptureToBytes()
    {
        // Allow changes to apply
        yield return new WaitForFixedUpdate();

        // Remove array (hopefully, we'll get new sensor data)
        sensorDataByteBuffer = new byte[0];

        yield return SaveCameraImageToByteBuffer(cameraForNextCapture);

        captureBytes = sensorDataByteBuffer.ToList();

        Debug.Log("Captured " + captureBytes.Count + " bytes of data.");

        captureBytesReady = true;
    }

    public IEnumerator SaveCameraImageToByteBuffer(Camera cam)
    {
        cam.Render();

        yield return null;

        RenderTexture activeRenderTexture = RenderTexture.active;
        RenderTexture.active = cam.targetTexture;

        Texture2D image = new Texture2D(cam.targetTexture.width, cam.targetTexture.height, TextureFormat.RGBA32, 0, true);
        image.ReadPixels(new Rect(0, 0, cam.targetTexture.width, cam.targetTexture.height), 0, 0);
        yield return null;

        image.Apply();
        yield return null;

        RenderTexture.active = activeRenderTexture;

        sensorDataByteBuffer = image.EncodeToPNG();
        Destroy(image);
        yield return null;
    }


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
        if (captureRequested)
        {
            StartCoroutine(CaptureToBytes());
            captureRequested = false;
        }

        if (ipPortTextNeedsToBeSet)
        {
            ipText.text = ipTextToSet;
            portText.text = portTextToSet;

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
            Byte[] bytes = new Byte[DATA_REQUEST_PACKET_LENGTH_BYTES];

            // Enter the listening loop.
            while (true)
            {
                Debug.Log("Waiting for a connection... ");

                // Perform a blocking call to accept requests.
                // You could also use server.AcceptSocket() here.
                TcpClient client = server.AcceptTcpClient();
                Debug.Log("Connected!");

                // Get a stream object for reading and writing
                NetworkStream stream = client.GetStream();

                int i;

                // Loop to receive all the data sent by the client.
                while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    Debug.Log("Received request (" + bytes.Length + "B) from client.");

                    // Parse request from client
                    DataRequestPacket drp;
                    bool parseSuccess = BytesToDataRequest(bytes, out drp);

                    Debug.Log("Parse success? " + parseSuccess);

                    // Trigger test image
                    captureBytesReady = false;
                    captureRequested = true;
                    captureRequestParameters = drp;

                    // Wait for image to be processed
                    while (!captureBytesReady)
                    {
                        System.Threading.Thread.Sleep(100);
                    }

                    byte[] msg;

                    // Make successful packet containing synthetic data payload
                    msg = MakeDataResponsePacketBytes(WorkRequestResponseType.SUCCESS, drp.responseID, captureBytes.ToArray());


                    stream.Write(msg, 0, msg.Length);
                    Debug.Log("Sent response (" + msg.Length + "B)");
                }

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

    byte[] MakeDataResponsePacketBytes(WorkRequestResponseType responseType, UInt32 responseID, byte[] sensorDataPayload)
    {
        // Prepare struct
        DataResponsePacket drp = new DataResponsePacket();
        drp.packetLength = sensorDataPayload.Length + DATA_RESPONSE_HEADER_LENGTH_BYTES;
        drp.responseType = responseType;
        drp.responseID = responseID;
        drp.payloadSize = (UInt32)sensorDataPayload.Length;
        drp.sensorDataPayload = sensorDataPayload;

        // Convert struct to bytes
        byte[] packetLengthBytes = BitConverter.GetBytes(drp.packetLength);
        byte[] statusBytes = new byte[1];
        statusBytes[0] = (byte)drp.responseType;
        byte[] responseIDBytes = BitConverter.GetBytes(drp.responseID);
        byte[] payloadSizeBytes = BitConverter.GetBytes(drp.payloadSize);

        // Reverse all data endianness if it's not in network byte order (big endian)
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(packetLengthBytes);
            Array.Reverse(responseIDBytes);
            Array.Reverse(payloadSizeBytes);
        }

        return CombineByteArrays(packetLengthBytes, statusBytes, responseIDBytes, payloadSizeBytes, drp.sensorDataPayload);
    }

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

    private bool BytesToDataRequest(byte[] bytes, out DataRequestPacket drp)
    {
        drp = new DataRequestPacket();

        // Make sure packet is the expected size
        if (bytes.Length != DATA_REQUEST_PACKET_LENGTH_BYTES)
        {
            return false;
        }

        // Parse packet
        int size = Marshal.SizeOf(drp);
        IntPtr ptr = Marshal.AllocHGlobal(size);

        Marshal.Copy(bytes, 0, ptr, size);

        drp = (DataRequestPacket)Marshal.PtrToStructure(ptr, drp.GetType());
        Marshal.FreeHGlobal(ptr);

        // Fix endianness if necessary
        if (BitConverter.IsLittleEndian)
        {
            drp.responseID = SwapEndian(drp.responseID);
        }

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
}