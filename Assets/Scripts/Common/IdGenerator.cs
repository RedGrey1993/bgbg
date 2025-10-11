using System.Threading;

public static class IdGenerator
{
    private static int _characterId = 0;
    private static int _pickupItemId = 0;

    public static uint NextCharacterId()
    {
        return (uint)Interlocked.Increment(ref _characterId);
    }

    public static uint NextPickupItemId()
    {
        return (uint)Interlocked.Increment(ref _pickupItemId);
    }

    public static void SetNextCharacterId(int nextId)
    {
        Interlocked.Exchange(ref _characterId, nextId);
    }

    // // TODO: 单机暂时用不到，联机模式Host挂了之后，同步状态给没挂的客户端作为新的Host可能会用到
    // public static void SetNextPickupItemId(int nextId)
    // {
    //     Interlocked.Exchange(ref _pickupItemId, nextId);
    // }
}