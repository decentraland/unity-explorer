namespace ECS.StreamableLoading.Cache.Disk.Cacheables
{
    public interface IHashKeyPayload
    {
        void Put(string value);

        void Put(int value);

        void Put(bool value);
    }
}
