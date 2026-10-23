using DiskSteward.Core.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace DiskSteward.Core.Tests.Caching;

public sealed class MemoryCacheExtensionsTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    [Fact]
    public async Task Concurrent_callers_share_one_load()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var loads = 0;
        var gate = new TaskCompletionSource<int>();

        var first = cache.GetOrLoadAsync("k", () => { loads++; return gate.Task; }, Hour);
        var second = cache.GetOrLoadAsync("k", () => { loads++; return gate.Task; }, Hour);
        gate.SetResult(7);

        Assert.Equal(7, await first);
        Assert.Equal(7, await second);
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task A_failed_load_is_evicted_so_the_next_call_loads_again()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());

        await Assert.ThrowsAsync<IOException>(() => cache.GetOrLoadAsync<int>("k", () => throw new IOException(), Hour));

        Assert.Equal(2, await cache.GetOrLoadAsync("k", () => Task.FromResult(2), Hour));
    }

    [Fact]
    public async Task A_late_failure_does_not_evict_a_newer_entry_under_the_same_key()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var slow = new TaskCompletionSource<int>();
        var stale = cache.GetOrLoadAsync("k", () => slow.Task, Hour);
        cache.Remove("k"); // the stale entry expires while its load is still running
        Assert.Equal(2, await cache.GetOrLoadAsync("k", () => Task.FromResult(2), Hour));

        slow.SetException(new IOException());
        await Assert.ThrowsAsync<IOException>(() => stale);

        Assert.Equal(2, await cache.GetOrLoadAsync("k", () => Task.FromResult(3), Hour));
    }
}
