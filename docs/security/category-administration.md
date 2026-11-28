# Category administration — issue #14

## Scope and dependencies

CategoryManager is embedded in AdminProducts.razor, above the existing product form.
It has its own EditForm and does not submit or reset the product/variant draft.
The page retains its Identity Admin authorization attribute. Every service operation
also rechecks the same administrator against current Identity roles and security stamps.

This implementation is based on PR #91, commit be25f0e. While that PR is open,
create the category branch from feat/product-administration and use that branch as
the initial PR base. Merge #91 first, then review/retarget the category PR to main.
Do not push category changes into feat/product-administration or update PR #91.

## Files

Replace only these existing files:

- Components/Pages/AdminProducts.razor
- Program.cs

Add these new files:

- Services/CategoryAdministrationService.cs
- Components/Shared/CategoryManager.razor
- Components/Shared/CategoryManager.razor.css
- wwwroot/js/categoryDialog.js
- PetShopApp.Tests/CategoryAdministrationServiceTests.cs
- docs/security/category-administration.md

The provided Program.cs also removes a duplicate ProductService registration found
in PR #91. Keep each application service registered once. Alternatively, manually
add AddScoped<CategoryAdministrationService>() next to ProductAdministrationService
and keep the remaining local Program.cs configuration.

## Behavior

- The category list includes names and current product counts.
- Names are required, trimmed, normalized to Unicode Form C and limited to 50 characters.
- Duplicate comparison ignores case, outer whitespace and equivalent composed Unicode text.
- Deliberate internal spacing is preserved. Embedded control characters are rejected.
- Create and Rename use the authorized CategoryAdministrationService.
- Renaming keeps the category ID and every Product.CategoryId relationship.
- The legacy Product.Category label is synchronized in the same transaction.
- Prices, stock, variants and historical Orders.SerializedItems are not rewritten.
- A stale rename/delete request must reload after another administrator changes the name.
- Categories with products have a disabled Delete button and cannot be deleted through service calls.
- The service rechecks product usage after confirmation, inside the write transaction.
- Cancel and Escape close the native dialog without calling the deletion operation.
- The modal is centered, white with dark text, and uses red for the destructive action.
- Native dialog behavior provides focus containment and focus return to the opener.
- Product form category choices and inventory labels update after category operations.
- Product creation, reassignment and deletion refresh category usage counts.
- Catalog category filters read the changed data when the catalog loads or is refreshed.
- Product and variant draft values are retained while a category is created or renamed.

## Persistence

No model change, new package or schema migration is needed. The existing Categories
and Products.CategoryId relationship already provides the required structure.
The AddProductStock migration from #91 remains required by the parent feature.

Short SQLite write transactions serialize duplicate-name checks and changes,
including concurrent create/rename requests. Unicode-aware comparison runs in .NET
rather than relying on SQLite's ASCII-only default uppercasing. All supported category
mutations must go through this service. Rename updates and their compatibility labels
commit together or roll back together.

## Local preparation

Stop the application before applying source files. Keep databases and local test
images outside commits. In PowerShell:

```powershell
# Refresh remote references without changing local files.
Set-Location E:\petshop-team
git fetch origin
if ($LASTEXITCODE -ne 0) { throw "Fetch failed." }

# Stop if tracked source changes still need to be committed or saved.
$pendingSource = @(git status --porcelain --untracked-files=no)
if ($pendingSource.Count -gt 0) {
    throw "Save the tracked source changes before creating the category branch."
}

# Create a separate branch based on the already published PR 91 source.
git switch --no-track -c feat/category-administration origin/feat/product-administration
if ($LASTEXITCODE -ne 0) { throw "Could not create the category branch." }
```

Extract the supplied ZIP into a separate folder. Copy only the eight listed files
into the matching repository paths. Do not delete or replace entire directories.
Keep existing image files, database files, configuration and unrelated JavaScript.
All new code comments are in English.

## Build and test

```powershell
# Compile the application and test projects after copying all files.
dotnet restore .\PetShopApp.slnx
if ($LASTEXITCODE -ne 0) { throw "Restore failed." }
dotnet build .\PetShopApp.slnx --no-restore
if ($LASTEXITCODE -ne 0) { throw "Build failed." }
dotnet test .\PetShopApp.slnx --no-build --no-restore
if ($LASTEXITCODE -ne 0) { throw "Tests failed." }
git --no-pager diff --check

# Select the existing isolated database already used for stock tests.
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
$env:ConnectionStrings__PetShopDatabase = "Data Source=E:\petshop-team\petshop-identity-authorization-test.db"

# Start the application without replacing the selected database settings.
dotnet run --project .\PetShopApp.csproj --no-build --no-launch-profile --urls "http://localhost:5177"
```

## Manual verification

Use the isolated development database, an administrator account and disposable data.
These manual cases require local browser execution; record actual results separately.

| ID | Action | Expected result |
|---|---|---|
| CAT-01 | Open /admin/products as an administrator. | Product Categories appears above the product form; names and product counts load. |
| CAT-02 | Create `QA Grooming`, then reload. | Success feedback, one persisted category and a new option in the product category selector. |
| CAT-03 | Try an empty name, spaces only, and ` qa GROOMING `. | Validation blocks each request; no duplicate row is created. |
| CAT-04 | Fill an unsaved product name, price, stock and a pending variant, then create another category. | Product/variant draft values remain; the new category becomes selectable. |
| CAT-05 | Assign a disposable product to QA Grooming and save the product. | Category product count updates; Delete is disabled for that category. |
| CAT-06 | Rename QA Grooming to QA Care, save and reload. | Same category ID and product association; inventory labels and product category selection show QA Care. |
| CAT-07 | Reload /catalog and filter by QA Care. | Renamed category is listed and the associated product is shown with unchanged price/stock. |
| CAT-08 | Reassign the disposable product to another category and save. | QA Care count becomes zero and Delete is enabled. |
| CAT-09 | Click Delete for QA Care, then Cancel; repeat using Escape. | Centered branded modal; both cancellations preserve the category. Tab stays in the dialog while open. |
| CAT-10 | Open Delete again and confirm. | Category is removed, success feedback appears, and refreshed product/catalog category choices omit it. |
| CAT-11 | Open an unused category's deletion dialog, then assign a product to it from another admin tab before confirming. | Service blocks deletion; the category and product relationship remain. |
| CAT-12 | Open a rename form in two admin tabs; save one name, then save the stale form. | Second save is rejected with a refresh message. |
| CAT-13 | Access /admin/products as a customer or anonymous visitor. | No category modification is allowed. |
| CAT-14 | Recheck product creation, variant stock and purchase totals. | Parent PR #91 behavior remains correct. |

The issue remains In progress until local manual verification and peer review are
complete and the PR is merged. Add `Closes #14` when opening the category PR.

## Automated coverage

Category tests cover real Identity authorization, revoked roles/stamps, validation,
Unicode-equivalent duplicates, persistence, fresh catalog lookups, usage counts,
relationship/stock/history preservation, stale requests, missing records, unused
category deletion, concurrent create/rename, transactional rename rollback and
server rendering of the category component. Native modal layout, focus and keyboard
interaction are included in the manual cases rather than claimed as browser-automated tests.

Prepared-source verification on 2026-11-28: 98 automated tests passed, zero failures or skips. The complete source builds; a clean compilation reports only the existing unrelated Breadcrumbs warning. git diff --check passes. Browser-only cases remain for local manual verification.

References: [SQLite transaction behavior](https://www.sqlite.org/lang_transaction.html) and [SQLite built-in scalar functions](https://www.sqlite.org/lang_corefunc.html).
