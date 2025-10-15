using System.Net.Sockets;
using System.Net;
using System.Threading;
using UnityEngine;

public class UDPSockets : MonoBehaviour
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
        newSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint ipep = new IPEndPoint(IPAddress.Loopback, 9050);
        newSocket.Bind(ipep);

        clientThread.Start();

        byte[] buffer = new byte[1024];

        

        newSocket.SendTo(System.Text.Encoding.UTF8.GetBytes("Hello from server"),ipep);

        while (!m_cancel)
        {
            EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            int receivedBytes = newSocket.ReceiveFrom(buffer, ref sender);

            if (receivedBytes > 0)
            {
                Debug.Log(System.Text.Encoding.UTF8.GetString(buffer, 0, receivedBytes));
                newSocket.SendTo(System.Text.Encoding.UTF8.GetBytes("Hello from server"), sender);
            }

            Thread.Sleep(10);
        }

    }

    void ClientProcess()
    {
        Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        IPEndPoint serverEp = new IPEndPoint(IPAddress.Loopback, 9050);

        clientSocket.SendTo(System.Text.Encoding.UTF8.GetBytes("Hello from client"), serverEp);

        byte[] buffer = new byte[1024];
        while (!m_cancel)
        {
            EndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            int receivedBytes = clientSocket.ReceiveFrom(buffer,ref sender);

            if (receivedBytes > 0)
            {
                Debug.Log(System.Text.Encoding.UTF8.GetString(buffer, 0, receivedBytes));

                clientSocket.SendTo(System.Text.Encoding.UTF8.GetBytes("Hello from client"),sender);
            }

            Thread.Sleep(10);
        }
    }
}
