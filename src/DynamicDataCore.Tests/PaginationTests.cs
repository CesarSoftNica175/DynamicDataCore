using DynamicDataCore.Common.Pagination;
using DynamicDataCore.Infrastructure.Implementation;
using Microsoft.EntityFrameworkCore;

namespace DynamicDataCore.Tests;

public class PaginationTests
{
    private static InMemoryDbContext<PaginableItem> BuildContext(string dbName)
    {
        var opts = new DbContextOptionsBuilder<InMemoryDbContext<PaginableItem>>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new InMemoryDbContext<PaginableItem>(opts);
    }

    private static async Task<InMemoryDbContext<PaginableItem>> SeedAsync(string dbName, int count = 100)
    {
        var ctx = BuildContext(dbName);
        ctx.Set<PaginableItem>().AddRange(
            Enumerable.Range(1, count).Select(i => new PaginableItem { Id = i, Name = $"P{i}" }));
        await ctx.SaveChangesAsync();
        return ctx;
    }

    // ── Offset ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Offset_Page1_Returns_First10()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<PaginableItem>(ctx);

        var result = await repo.RetrievePagedByOffsetAsync(
            new OffsetPageRequest(Page: 1, PerPage: 10),
            orderBy: p => p.Id);

        Assert.Equal(10, result.Items.Count);
        Assert.Equal(1, result.Items[0].Id);
        Assert.Equal(10, result.Items[^1].Id);
    }

    [Fact]
    public async Task Offset_Page3_Returns_Items21To30()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<PaginableItem>(ctx);

        var result = await repo.RetrievePagedByOffsetAsync(
            new OffsetPageRequest(Page: 3, PerPage: 10),
            orderBy: p => p.Id);

        Assert.Equal(10, result.Items.Count);
        Assert.Equal(21, result.Items[0].Id);
        Assert.Equal(30, result.Items[^1].Id);
    }

    [Fact]
    public async Task Offset_Metadata_IsCorrect()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<PaginableItem>(ctx);

        var result = await repo.RetrievePagedByOffsetAsync(
            new OffsetPageRequest(Page: 2, PerPage: 10),
            orderBy: p => p.Id);

        Assert.Equal(100, result.Metadata.Total);
        Assert.Equal(10, result.Metadata.LastPage);
        Assert.Equal(2, result.Metadata.CurrentPage);
        Assert.Equal(11, result.Metadata.From);
        Assert.Equal(20, result.Metadata.To);
    }

    [Fact]
    public async Task Offset_LastPage_HasNoNextCursor()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<PaginableItem>(ctx);

        var result = await repo.RetrievePagedByOffsetAsync(
            new OffsetPageRequest(Page: 10, PerPage: 10),
            orderBy: p => p.Id);

        Assert.Null(result.NextCursor);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public async Task Offset_NotLastPage_HasNextCursor()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<PaginableItem>(ctx);

        var result = await repo.RetrievePagedByOffsetAsync(
            new OffsetPageRequest(Page: 1, PerPage: 10),
            orderBy: p => p.Id);

        Assert.NotNull(result.NextCursor);
        Assert.True(result.HasNextPage);
    }

    // ── Keyset ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Keyset_After50_Ascending_Returns51To60()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<PaginableItem>(ctx);

        var result = await repo.RetrievePagedByKeysetAsync(
            new KeysetPageRequest<int>(After: 50, PerPage: 10),
            keySelector: p => p.Id);

        Assert.Equal(10, result.Items.Count);
        Assert.Equal(51, result.Items[0].Id);
        Assert.Equal(60, result.Items[^1].Id);
    }

    [Fact]
    public async Task Keyset_Descending_ReturnsBelowPivot()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<PaginableItem>(ctx);

        var result = await repo.RetrievePagedByKeysetAsync(
            new KeysetPageRequest<int>(After: 50, PerPage: 10, Direction: SortDirection.Descending),
            keySelector: p => p.Id);

        Assert.Equal(10, result.Items.Count);
        // Descending: IDs just below 50 — 49, 48, ..., 40
        Assert.Equal(49, result.Items[0].Id);
        Assert.Equal(40, result.Items[^1].Id);
    }

    [Fact]
    public async Task Keyset_NullAfter_StartsFromBeginning()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<PaginableItem>(ctx);

        // default(int) = 0; WHERE Id > 0 returns all since IDs start at 1
        var result = await repo.RetrievePagedByKeysetAsync(
            new KeysetPageRequest<int>(After: default, PerPage: 5),
            keySelector: p => p.Id);

        Assert.Equal(5, result.Items.Count);
        Assert.Equal(1, result.Items[0].Id);
    }

    [Fact]
    public async Task Keyset_HasNextPage_WhenMoreItemsExist()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<PaginableItem>(ctx);

        var result = await repo.RetrievePagedByKeysetAsync(
            new KeysetPageRequest<int>(After: default, PerPage: 10),
            keySelector: p => p.Id);

        Assert.True(result.HasNextPage);
        Assert.NotNull(result.NextCursor);
    }

    [Fact]
    public async Task Keyset_NoNextPage_WhenAtEnd()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<PaginableItem>(ctx);

        // After 91 with perPage 10 → 92..100 (9 items) → no next
        var result = await repo.RetrievePagedByKeysetAsync(
            new KeysetPageRequest<int>(After: 91, PerPage: 10),
            keySelector: p => p.Id);

        Assert.Equal(9, result.Items.Count);
        Assert.False(result.HasNextPage);
        Assert.Null(result.NextCursor);
    }

    // ── Seek ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Seek_LastSeenId25_Returns26To30()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<PaginableItem>(ctx);

        var result = await repo.RetrievePagedBySeekAsync(
            new SeekPageRequest<int>(LastSeenId: 25, PerPage: 5),
            keySelector: p => p.Id);

        Assert.Equal(5, result.Items.Count);
        Assert.Equal(26, result.Items[0].Id);
        Assert.Equal(30, result.Items[^1].Id);
    }

    [Fact]
    public async Task Seek_NullLastSeenId_StartsFromFirst()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<PaginableItem>(ctx);

        // default(int) = 0; WHERE Id > 0 returns all since IDs start at 1
        var result = await repo.RetrievePagedBySeekAsync(
            new SeekPageRequest<int>(LastSeenId: default, PerPage: 5),
            keySelector: p => p.Id);

        Assert.Equal(5, result.Items.Count);
        Assert.Equal(1, result.Items[0].Id);
    }

    [Fact]
    public async Task Seek_DoesNotIncludePivotItem()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<PaginableItem>(ctx);

        var result = await repo.RetrievePagedBySeekAsync(
            new SeekPageRequest<int>(LastSeenId: 10, PerPage: 5),
            keySelector: p => p.Id);

        Assert.DoesNotContain(result.Items, i => i.Id == 10);
        Assert.Equal(11, result.Items[0].Id);
    }

    // ── Cursor ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cursor_FirstPage_HasNextCursorWhenMoreExist()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<PaginableItem>(ctx);

        var page1 = await repo.RetrievePagedByCursorAsync(
            new CursorPageRequest(Cursor: null, PerPage: 10),
            keySelector: p => p.Id);

        Assert.Equal(10, page1.Items.Count);
        Assert.NotNull(page1.NextCursor);
        Assert.True(page1.HasNextPage);
    }

    [Fact]
    public async Task Cursor_SecondPage_ReturnsNextWindow()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<PaginableItem>(ctx);

        var page1 = await repo.RetrievePagedByCursorAsync(
            new CursorPageRequest(Cursor: null, PerPage: 10),
            keySelector: p => p.Id);

        var page2 = await repo.RetrievePagedByCursorAsync(
            new CursorPageRequest(Cursor: page1.NextCursor, PerPage: 10),
            keySelector: p => p.Id);

        Assert.Equal(10, page2.Items.Count);
        // Page 2 should start after page 1 ended
        Assert.True(page2.Items[0].Id > page1.Items[^1].Id);
    }

    [Fact]
    public async Task Cursor_InvalidBase64_ReturnsFirstPage()
    {
        using var ctx = await SeedAsync(Guid.NewGuid().ToString());
        var repo = new GenericRepositoryImpl<PaginableItem>(ctx);

        // An invalid cursor decodes to default(int) = 0, which behaves like "start from beginning"
        var page = await repo.RetrievePagedByCursorAsync(
            new CursorPageRequest(Cursor: "!!!invalid!!!", PerPage: 5),
            keySelector: p => p.Id);

        Assert.Equal(5, page.Items.Count);
        Assert.Equal(1, page.Items[0].Id);
    }
}
