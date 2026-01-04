using UnityEngine;

public class PendingPacket
{
    public int sequenceId;
    public float sendTime;
    public byte[] serializedData;
    public System.Net.EndPoint target;
    public int retryCount;

    public PendingPacket(int seq, float time, byte[] data, System.Net.EndPoint target)
    {
        sequenceId = seq;
        sendTime = time;
        serializedData = data;
        this.target = target;
        retryCount = 0;
    }
}
