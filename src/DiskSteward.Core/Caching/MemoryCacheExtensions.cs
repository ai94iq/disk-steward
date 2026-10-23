namespace DiskSteward.Core.Caching;

public static class MemoryCacheExtensions
{
    // Caches the task, not the result: concurrent callers share one load, and failures are evicted.
    // The load must not depend on a caller's token; callers use .WaitAsync(ct) to stop waiting.
    // A failure evicts only its own entry: if that one expired mid-load and a newer load took
    // the key, the newer one stays.
    public static Task<T> GetOrLoadAsync<T>(this IMemoryCache cache, string key,
        Func<Task<T>> load, TimeSpan timeToLive, long size = 1)
    {
        var lazy = cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = timeToLive;
            entry.Size = size;
            Lazy<Task<T>>? self = null;
            self = new Lazy<Task<T>>(async () =>
            {
                try
                {
                    return await load().ConfigureAwait(false);
                }
                catch
                {
                    if (cache.TryGetValue(key, out var current) && ReferenceEquals(current, self)) cache.Remove(key);
                    throw;
                }
            });
            return self;
        })!;
        return lazy.Value;
    }
}
