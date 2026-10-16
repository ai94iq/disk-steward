namespace DiskSteward.App.Tests;

public sealed class PageViewModelTests
{
    [Fact]
    public async Task Failed_load_shows_error_state()
    {
        var page = new FakePage(_ => throw new InvalidOperationException("boom"));
        await page.EnsureLoadedAsync();
        Assert.Equal(LoadState.Error, page.State);
        Assert.False(string.IsNullOrEmpty(page.ErrorMessage));
    }

    [Fact]
    public async Task Nothing_to_show_gives_empty_state()
    {
        var page = new FakePage(_ => Task.FromResult(false));
        await page.EnsureLoadedAsync();
        Assert.Equal(LoadState.Empty, page.State);
    }

    [Fact]
    public async Task Quiet_load_failure_after_content_keeps_the_page_loaded()
    {
        var page = new FakePage(_ => Task.FromResult(true));
        await page.EnsureLoadedAsync();
        page.QuietFail(new InvalidOperationException("boom"));
        Assert.Equal(LoadState.Loaded, page.State);
        Assert.Null(page.ErrorMessage);
    }

    [Fact]
    public void Quiet_load_failure_before_content_shows_the_error_state()
    {
        var page = new FakePage(_ => Task.FromResult(true));
        page.QuietFail(new InvalidOperationException("boom"));
        Assert.Equal(LoadState.Error, page.State);
        Assert.False(string.IsNullOrEmpty(page.ErrorMessage));
    }

    [Fact]
    public void Quiet_load_completion_sets_the_content_state()
    {
        var page = new FakePage(_ => Task.FromResult(true));
        page.QuietComplete(hasContent: false);
        Assert.Equal(LoadState.Empty, page.State);
    }

    private sealed class FakePage(Func<CancellationToken, Task<bool>> load) : PageViewModel(NullLogger.Instance)
    {
        protected override Task<bool> LoadCoreAsync(CancellationToken ct) => load(ct);

        public void QuietComplete(bool hasContent) => MarkQuietLoadCompleted(hasContent);

        public void QuietFail(Exception ex) => MarkQuietLoadFailed(ex);
    }
}
