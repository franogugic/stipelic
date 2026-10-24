using CreatorPlatform.Shared.Application.Workspaces;
using CreatorPlatform.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CreatorPlatform.Products.Infrastructure.Workspaces;

/// <summary>Deletes the products nothing was sold of. A product with kept (paid or refunded) orders stays as a tombstone
/// those orders point at: its name says what was sold; its description, image and file go.</summary>
public sealed class ProductsWorkspaceDataPurger(CreatorPlatformDbContext db) : IWorkspaceDataPurger
{
    public int Order => WorkspacePurgeOrder.Products;

    public async Task PurgeAsync(int creatorId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlAsync($"""
            DELETE FROM products.products AS p
            WHERE p."CreatorId" = {creatorId}
              AND NOT EXISTS (SELECT 1 FROM orders.orders AS o WHERE o."ProductId" = p."Id")
            """, ct);
        await db.Database.ExecuteSqlAsync($"""
            UPDATE products.products
            SET "Description" = NULL, "ThumbnailUrl" = NULL, "Status" = 'Archived',
                "FileBlobPath" = NULL, "FileName" = NULL, "FileSizeBytes" = NULL, "FileContentType" = NULL,
                "FileUploadedAt" = NULL
            WHERE "CreatorId" = {creatorId}
            """, ct);
    }
}
