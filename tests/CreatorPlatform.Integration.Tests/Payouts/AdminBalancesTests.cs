using CreatorPlatform.Integration.Tests.Infrastructure;
using CreatorPlatform.Payouts.Application.Dtos;
using CreatorPlatform.Payouts.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Integration.Tests.Payouts;

/// <summary>The admin's "balances to pay out" list includes deleted (Disabled) workspaces that are still owed money.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AdminBalancesTests
{
    private const int MinCents = 1_000;

    private readonly PostgresFixture _fixture;
    private readonly TestData _data;

    public AdminBalancesTests(PostgresFixture fixture)
    {
        _fixture = fixture;
        _data = new TestData(fixture);
    }

    [Fact]
    public async Task ADeletedWorkspaceWithABalance_IsListedWithItsStatus()
    {
        var deleted = await CreateBankTransferCreatorAsync("Disabled", balanceCents: 5_000);
        var active = await CreateBankTransferCreatorAsync("Active", balanceCents: 2_500);

        var balances = await ListAsync();

        var deletedRow = Assert.Single(balances, b => b.CreatorPublicId == deleted);
        Assert.Equal(("Disabled", 5_000, "Eur"), (deletedRow.WorkspaceStatus, deletedRow.BalanceCents, deletedRow.Currency));
        Assert.Equal("Active", Assert.Single(balances, b => b.CreatorPublicId == active).WorkspaceStatus);
    }

    [Fact]
    public async Task ADeletedWorkspaceBelowTheMinimum_IsStillNotListed()
    {
        var settled = await CreateBankTransferCreatorAsync("Disabled", balanceCents: MinCents - 1);

        Assert.DoesNotContain(await ListAsync(), b => b.CreatorPublicId == settled);
    }

    private async Task<List<CreatorBalanceSummaryDto>> ListAsync()
    {
        await using var db = _fixture.CreateDbContext();
        // The DB is shared by every test: a generous limit so this test's rows are always in the page.
        return await new LedgerEntryRepository(db).GetBalancesForPayoutAsync(MinCents, limit: 10_000, CancellationToken.None);
    }

    private async Task<Guid> CreateBankTransferCreatorAsync(string status, int balanceCents)
    {
        var creator = await _data.CreateCreatorAsync();
        await using var db = _fixture.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"""
            UPDATE creators.creators SET "PayoutMode" = 'BankTransfer', "CountryCode" = 'RS', "Status" = {status}
            WHERE "Id" = {creator.CreatorId}
            """);
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO payouts.ledger_entries ("PublicId", "CreatorId", "Type", "AmountCents", "Currency", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {creator.CreatorId}, 'Adjustment', {balanceCents}, 'Eur', {DateTimeOffset.UtcNow})
            """);
        return (await db.Database.SqlQuery<Guid>($"""
            SELECT "PublicId" AS "Value" FROM creators.creators WHERE "Id" = {creator.CreatorId}
            """).ToListAsync()).Single();
    }
}
