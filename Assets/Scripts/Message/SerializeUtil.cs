
#if PROTOBUF
using Google.Protobuf;
#endif

public class SerializeUtil
{
#if PROTOBUF
    public static void Serialize<T>(T message, out byte[] data) where T : IMessage<T>
    {
        data = message.ToByteArray();
    }
#else
    public static void Serialize<T>(T message, out byte[] data) where T : class
    {
        data = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));
    }
#endif

#if PROTOBUF
    public static void Deserialize<T>(byte[] data, out T message) where T : IMessage<T>, new()
    {
        message = new T();
        message.MergeFrom(data);
    }
#else
    public static void Deserialize<T>(byte[] data, out T message) where T : class
    {
        message = JsonUtility.FromJson<T>(System.Text.Encoding.UTF8.GetString(data));
    }
#endif
}