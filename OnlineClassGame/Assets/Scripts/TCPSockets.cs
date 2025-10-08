using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Threading;

public class TCPSockets : MonoBehaviour
{

    Socket newSocket;

    Thread serverThread;
    Thread clientThread;

    bool m_cancel = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        clientThread = new Thread(ClientProcess);
        serverThread = new Thread(ServerProcess);

        serverThread.Start();
    }

    // Update is called once per frame
    private void OnDestroy()
    {
        m_cancel = true;
        clientThread.Abort();
        serverThread.Abort();
    }

    void ServerProcess()
    {
        newSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint ipep = new IPEndPoint(IPAddress.Loopback, 9050);
        newSocket.Bind(ipep);

        newSocket.Listen(10);

        clientThread.Start();

        Socket clientSocket = newSocket.Accept();

        clientSocket.Send(System.Text.Encoding.UTF8.GetBytes("Hello from server"));

        byte[] buffer = new byte[1024];

        while (!m_cancel)
        {
            int receivedBytes = clientSocket.Receive(buffer);   

            if (receivedBytes > 0)
            {
                Debug.Log(System.Text.Encoding.UTF8.GetString(buffer, 0, receivedBytes));
                clientSocket.Send(System.Text.Encoding.UTF8.GetBytes("Hello from server"));
            }

            Thread.Sleep(10);
        }

    }

    void ClientProcess()
    {
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint serverEp = new IPEndPoint(IPAddress.Loopback, 9050);

        clientSocket.Connect(serverEp);
        byte[] buffer = new byte[1024];
        while (!m_cancel)
        {
            int receivedBytes = clientSocket.Receive(buffer);

            if (receivedBytes > 0)
            {
                Debug.Log(System.Text.Encoding.UTF8.GetString(buffer, 0, receivedBytes));

                clientSocket.Send(System.Text.Encoding.UTF8.GetBytes("Hello from client"));
            }

            Thread.Sleep(10);
        }
    }
}
