# Product inventory and checkout

## Behavior

- `Product.StockQuantity` holds inventory for products without priced variants.
- Labels in `AvailableOptionsRaw` share this product stock pool and base price.
- `ProductVariant.StockQuantity` holds independent inventory for each priced option.
- Priced variants take priority; the parent product count is not sold when variants exist.
- Zero is a valid count and means unavailable. Negative counts are rejected by services and SQLite constraints.
- Adding, changing, removing, clearing or abandoning a cart never changes database inventory.
- Cart quantities are limited against freshly loaded stock, including units already selected from the same pool.
- Cart selections are not reservations. Another customer can buy the remaining units first.
- Checkout groups repeated lines and CSV selections sharing one pool, checks inventory and decrements it conditionally.
- One explicit transaction includes every decrement and the saved order. Any failure rolls back all stock changes.
- Stock and order snapshots use trusted database data. Historical names, quantities, prices and totals are preserved.
- Account cart isolation and HTTP sign-in/sign-out remain unchanged.
- Orders are created in Processing status; no payment gateway or automatic cancellation/restocking is added here.

## Administration

Products without priced variants show a Stock quantity field in the main form.
For a priced variant, enter Option, Unit Price and Stock quantity, then use Add Variant.
All three fields may be left blank for a product without variants; partially completed rows are rejected.
A completed row is saved with Create/Update even if Add Variant was not clicked.

Saved variants have their own Stock quantity input and Update stock button.
This button saves only that option's count; Update Product saves the parent form.
A sale while a form is open causes a stale-stock error rather than restoring sold units.
Refresh List and reopen the product before retrying with its current stock.
Only current Identity administrators may create, edit, replenish or delete inventory.

## Storefront

Cards show OUT OF STOCK only when no purchasable variant has stock, or when a plain product has zero stock.
Sold-out variant labels remain visible and disabled; available variants can still be selected.
Quick Add selects the requested available option, or the first available option in numeric weight order.
Images, secondary-image carousel, descriptions and promotional prices are retained.

Quick Add, product detail, cart increases and Buy Again query fresh product data before an addition.
Failed additions show feedback rather than a success message or closing Quick Add.
Checkout shows an insufficient-stock message with a link back to the bag and retains the form/cart.
Catalog cards refresh when loaded or when Quick Add obtains a new snapshot; this feature does not broadcast changes to all open browser tabs.

## Migration and existing data

Apply `20261128110724_AddProductStock` after the Identity infrastructure migration.
The migration adds non-null integer stock columns with default zero and nonnegative constraints.
It does not guess inventory counts or rewrite existing products, options, accounts or order snapshots.
All existing catalog products/options therefore begin OUT OF STOCK until an administrator enters real counts.

Test on the existing isolated development database first. Stop the server before copying or backing up SQLite files.
Do not copy a live SQLite .db file while its WAL/SHM files may contain committed changes.
The generated migration and its designer/snapshot are included; do not generate a second AddProductStock migration.

## Manual application in the local repository

1. Stop the application and remain on `feat/product-administration`.
2. Preserve the current source changes in Git or an external backup.
3. Open the supplied complete source files and manually replace the corresponding files in the repository.
4. Keep exactly one `ProductAdministrationServiceTests` class. If the old filename is
   `PetShopApp.Tests/ProductAdminstrationServiceTests.cs`, rename it to
   `ProductAdministrationServiceTests.cs` before replacing its contents. Do not keep both copies.
5. Keep `builder.Services.AddScoped<ProductAdministrationService>();` in Program.cs exactly once.
   No other Program.cs changes are required for stock.
6. Copy the two generated AddProductStock migration files and the updated model snapshot together.
7. Restore, build, update the isolated database and run the test suite using the commands below.
8. Start the server, enter real test stock and perform the manual scenarios.

```powershell
# Open the repository and select only the existing isolated development database.
Set-Location E:\petshop-team
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
$stockTestConnection = "Data Source=E:\petshop-team\petshop-identity-authorization-test.db"
$env:ConnectionStrings__PetShopDatabase = $stockTestConnection

# Compile the changed application and test suite.
dotnet restore .\PetShopApp.slnx
if ($LASTEXITCODE -ne 0) { throw "Restore failed." }
dotnet build .\PetShopApp.slnx --no-restore
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

# Use the existing local EF Core tool to apply the supplied migration.
dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw "Tool restore failed." }
dotnet tool run dotnet-ef database update 20261128110724_AddProductStock --project .\PetShopApp.csproj --context ApplicationDbContext --connection "$stockTestConnection" --no-build
if ($LASTEXITCODE -ne 0) { throw "Migration failed." }

# Run isolated automated tests before testing through the browser.
dotnet test .\PetShopApp.slnx --no-build --no-restore
if ($LASTEXITCODE -ne 0) { throw "Tests failed." }
git --no-pager diff --check

# Start the application; keep this terminal open.
dotnet run --project .\PetShopApp.csproj --no-build --no-launch-profile --urls "http://localhost:5177"
```

## Manual scenarios

Use disposable products in the isolated database. Record actual results separately.

| ID | Action | Expected result |
|---|---|---|
| STOCK-01 | Create a plain collar with stock 3. | Stock persists after Edit/F5; the product can be added. |
| STOCK-02 | Add 2 units, then attempt 2 more. | Cart stays at 2; the extra request is blocked; database stock remains 3. |
| STOCK-03 | Remove/clear these selections. | Database stock remains 3; the cart allowance becomes 3 again. |
| STOCK-04 | Purchase all 3 units. | One saved order, stock 0 and OUT OF STOCK on the refreshed catalog; addition disabled. |
| STOCK-05 | Create 2.2 lb with stock 0 and 11 lb with stock 2. | First variant is marked unavailable; 11 lb is selected in Quick Add and uses its promotional price. |
| STOCK-06 | Purchase one 11 lb unit. | Only 11 lb drops from 2 to 1; the other variant and parent count are unchanged. |
| STOCK-07 | Create S/M CSV options with shared stock 3; add S=2 and M=2. | The second request is rejected because both labels share the same pool. |
| STOCK-08 | Add the last unit in two separate browsers/accounts, then checkout both. | Both may select it; only one order succeeds. The other retains its form/cart and receives a stock message. |
| STOCK-09 | Replenish a sold-out product/variant. | After saving the count and refreshing the catalog, purchasing is enabled again. |
| STOCK-10 | Leave an admin edit open while another account purchases, then save the old count. | Stale update is rejected; the sold quantity is not restored. |
| STOCK-11 | Use Buy Again for an unavailable item or excessive historical quantity. | Warning shown; no false success or extra cart quantity. Original order remains unchanged. |
| STOCK-12 | Enter negative, fractional or incomplete inventory values. | Invalid input is blocked without partially creating a product or variant. |
| STOCK-13 | Repeat the image/carousel, discount and logout/cart-isolation checks. | Existing behavior remains correct. |

## Verification performed in the prepared source

The automated suite covers cart limits, shared/independent pools, safe variant values,
zero stock, checkout snapshots, repeated lines, transactional rollback, negative database
writes, distinct-customer concurrent checkout, migration preservation and server-rendered Quick Add states.
Browser interactions and visual appearance still require the manual scenarios above.

Prepared-source result on 2026-11-28: build successful (one existing Breadcrumbs warning), 69 automated tests passed, zero failures/skips, and git diff --check passed.
