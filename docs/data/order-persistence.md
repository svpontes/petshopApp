# Order persistence and migration — issue #19

## Scope and review

This change makes the existing order database mapping explicit and verifies its
migration and deletion rules. It does not change checkout processing, payment
handling, order-status administration or the representation used by customer history.
Steven must review the order mapping and snapshot decision before issue #19 is closed.

The source is based on PR #92, commit c14f1ca, which includes PR #91. While those
pull requests remain open, create this branch from feat/category-administration and
add the new pull request to the existing stack. Lower layers must reach main before
this persistence layer; verify GitHub's automatic retargeting after each merge.

## Existing representation retained

Order is the persisted aggregate in the Orders table. Its purchased OrderItem values
remain in the required SerializedItems JSON column. OrderItems is a NotMapped helper,
and OrderItem is a snapshot value object rather than a separate EF entity/table.
Do not add Include(order => order.OrderItems) to queries; load Orders and read the helper.
SQLite supports relational item tables, but JSON is the representation already used
by the merged order service and history pages. This migration does not convert it.

Snapshots retain product IDs, names, image URLs, selected labels, quantities and actual
purchase-time decimal unit prices. They have no foreign keys to current products or
variants. Editing or deleting the live catalog therefore cannot change or cascade-delete
the historical values. Order totals and contact details remain purchase-time values.

A getter returns a detached list. Mutating only that temporary list does not save it;
assign the complete OrderItems list if an intentional snapshot write is ever required.
Status-only updates must leave SerializedItems untouched. Malformed legacy payloads
remain preserved as raw strings and retain the existing empty-list fallback when read;
repairing historical data is a separate, explicitly reviewed operation.

## Explicit mapping

Data/OrderConfiguration.cs configures:

- Orders table and generated primary key.
- Required date, decimal total, status, name, email, address and snapshot columns.
- Existing TEXT storage for SQLite date/decimal columns.
- Nullable UserId for existing unassociated historical orders.
- An explicit relationship to User.OrderHistory with ClientNoAction.
- An owner/date/ID index matching newest-first authorized history queries.

Nullable historical ownership does not enable guest checkout. The existing creation
service still requires an authenticated Identity account and assigns that account's ID.
New Order defaults use UTC; no existing timestamp is converted or reinterpreted.

ClientNoAction keeps EF from clearing the UserId of tracked orders when their owner is
deleted. The noncascading SQLite foreign key then rejects deleting an account with orders.
This is verified with history both loaded and unloaded. Accounts without orders can be
deleted. Explicitly clearing relationships, anonymization and account-erasure workflows
are outside this task and need a separate retention/privacy design.

## Migration review

Migration: 20261128125842_ConfigureOrderPersistence.
Prerequisite: 20261128110724_AddProductStock and the earlier Identity/catalog migrations.

Up drops IX_Orders_UserId and creates the nonunique index
IX_Orders_UserId_OrderDate_Id with ascending owner and descending date/ID columns.
Down restores the original owner-only index. No table, column, order snapshot, account,
product or category data is dropped, backfilled, normalized or reserialized.
The explicit client-side delete behavior uses the existing noncascading database FK.

SQLite does not enforce decimal scale merely from EF precision metadata. This mapping
preserves existing decimal values rather than rounding purchase-time data. Business
validation and promotion rounding remain in the order creation service.

## Files to apply manually

Replace these existing files only:

- Data/ApplicationDbContext.cs
- Data/README.md
- Model/Order.cs
- Model/OrderItem.cs
- Model/README.md
- Migrations/ApplicationDbContextModelSnapshot.cs

Add these new files:

- Data/OrderConfiguration.cs
- Migrations/20261128125842_ConfigureOrderPersistence.cs
- Migrations/20261128125842_ConfigureOrderPersistence.Designer.cs
- PetShopApp.Tests/OrderPersistenceTests.cs
- docs/data/order-persistence.md

No Program.cs registration or new package is required. Copy the supplied migration,
its designer and the updated snapshot together; do not generate a second migration.

## Local branch preparation

Stop the application, then use PowerShell:

```powershell
# Refresh the published branches and protect pending tracked work.
Set-Location E:\petshop-team
git fetch origin
if ($LASTEXITCODE -ne 0) { throw "Fetch failed." }
$pendingSource = @(git status --porcelain --untracked-files=no)
if ($pendingSource.Count -gt 0) {
    throw "Save the tracked changes before creating the persistence branch."
}

# Keep this work separate from the published category PR.
git switch --no-track -c fix/order-persistence origin/feat/category-administration
if ($LASTEXITCODE -ne 0) { throw "Could not create the persistence branch." }
```

Extract the ZIP outside the repository. Copy only the listed files into their matching
folders. Do not delete directories or replace unrelated configuration, images or databases.

## Preserve a test-database backup

Stop the application and close DB Browser first. The following copies the test database
and any companion WAL/SHM files together into a separate folder, retaining their filenames.
Do not copy a live SQLite database while another process can still write to it.
Keep this backup folder outside Git commits.

```powershell
# Back up the existing isolated database before migration.
$testDbPath = "E:\petshop-team\petshop-identity-authorization-test.db"
if (!(Test-Path -LiteralPath $testDbPath)) { throw "Verify the isolated database path." }
$backupFolder = Join-Path "E:\petshop-team\db-backups" ("before-order-persistence-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
foreach ($suffix in @("", "-wal", "-shm")) {
    $sourceFile = "$testDbPath$suffix"
    if (Test-Path -LiteralPath $sourceFile) {
        Copy-Item -LiteralPath $sourceFile -Destination $backupFolder
    }
}
```

## Build, apply the supplied migration and test

```powershell
# Select the isolated development database explicitly.
Set-Location E:\petshop-team
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
$orderTestConnection = "Data Source=E:\petshop-team\petshop-identity-authorization-test.db"
$env:ConnectionStrings__PetShopDatabase = $orderTestConnection

# Compile the changed mapping and all test projects.
dotnet restore .\PetShopApp.slnx
if ($LASTEXITCODE -ne 0) { throw "Restore failed." }
dotnet build .\PetShopApp.slnx --no-restore
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

# Apply the reviewed migration instead of creating another one.
dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw "Tool restore failed." }
dotnet tool run dotnet-ef database update 20261128125842_ConfigureOrderPersistence --project .\PetShopApp.csproj --context ApplicationDbContext --connection "$orderTestConnection" --no-build
if ($LASTEXITCODE -ne 0) { throw "Migration failed." }

# Run isolated tests and confirm the selected database's migration history.
dotnet test .\PetShopApp.slnx --no-build --no-restore
if ($LASTEXITCODE -ne 0) { throw "Tests failed." }
dotnet tool run dotnet-ef migrations list --project .\PetShopApp.csproj --context ApplicationDbContext --connection "$orderTestConnection" --no-build
if ($LASTEXITCODE -ne 0) { throw "Could not list migrations." }
git --no-pager diff --check
```

The automated migration tests create their own temporary databases, migrate from
scratch and upgrade an existing database. Do not delete the shared/local database to
simulate an empty database, and do not run rollback against the database used for manual
verification merely because the automated suite tests that path.

## Manual evidence for the team

Record the branch/commit, date, tester and actual results. Before migration, note the
existing category/product/order counts and one order's ID, owner ID, purchased name,
option, quantity, price and total. Do not include credentials or customer details in screenshots.

| ID | Action | Expected result |
|---|---|---|
| PERSIST-01 | Apply ConfigureOrderPersistence to the existing isolated database and list migrations. | Migration completes and no pending migration remains. |
| PERSIST-02 | Inspect the catalog and inventory after migration. | Existing product/category IDs, names, variants, images and stock are unchanged. |
| PERSIST-03 | Sign in to the owner of an existing test order and view history. | The same order ID, option, quantity, purchased price and total remain. |
| PERSIST-04 | Place a new test order with a variant and a plain product. | A complete order is saved; the correct account sees both items and their purchase-time data. |
| PERSIST-05 | Change a disposable purchased product's live name and price, then view the saved order. | Historical name, option, quantities, unit prices and order total do not change. |
| PERSIST-06 | Delete that disposable catalog product and revisit history. | The purchase snapshot remains visible; Buy Again reports the missing product. |
| PERSIST-07 | Run database update for the same migration a second time. | No changes are applied and existing records remain intact. |
| PERSIST-08 | Run the full automated suite. | Fresh creation, upgrade, rollback, required fields, FK protection and preservation tests pass. |

After the commands succeed, start the application with the same environment:

```powershell
# Run the already compiled application against the selected isolated database.
dotnet run --project .\PetShopApp.csproj --no-build --no-launch-profile --urls "http://localhost:5177"
```

Account deletion with orders is exercised safely by automated tests using disposable
SQLite files. No account deletion UI is introduced by this task; do not remove real
accounts manually to reproduce this test.

## Automated evidence

OrderPersistenceTests verifies explicit EF metadata, complete order retrieval, snapshot
independence from product/profile edits, product/category deletion, loaded/unloaded owner
delete protection, valid deletion without orders, legacy ownerless records, invalid-owner
FK rejection, NOT NULL fields, raw legacy JSON preservation, fresh migrations and
upgrade/rerun/rollback preservation of every stored column in five existing tables.

The migration snapshot comparison includes raw Users values only inside disposable test
files. It never logs or reads a developer's account database.

## References

- [EF Core cascade-delete behavior](https://learn.microsoft.com/en-us/ef/core/saving/cascade-delete)
- [EF Core DeleteBehavior](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.deletebehavior?view=efcore-10.0)

Prepared-source verification on 2026-11-28: 116 automated tests passed, zero failures/skips; build successful with only the existing Breadcrumbs warning on a clean compilation. git diff --check passes. Local application of the migration and manual browser evidence still require the steps above.
