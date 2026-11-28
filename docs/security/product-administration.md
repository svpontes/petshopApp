# Product administration

## Responsibilities

ProductAdministrationService owns protected inventory reads, product creation,
product updates, product deletion and variant creation. Each operation requires a
current administrator account through AuthService.IsAuthorizedAsync. The same
account is rechecked after awaited queries and before saving a mutation.

AdminProducts uses detached editing models and does not query or save database
entities directly. Each service operation creates and disposes its own context.
Only allowed scalar fields are copied from a submitted product; navigation
properties and submitted variant collections are not attached.

## Validation

- Product names are trimmed, required, and limited to 200 characters.
- Product prices cannot be negative. Zero is allowed.
- The selected animal type must exist.
- A category is optional; a selected category must exist.
- Legacy category text is derived from the selected saved category.
- Clearing a category also clears the legacy text value.
- Variant labels are trimmed, required, and limited to 100 characters.
- Variant prices must be greater than zero.
- Product and variant stock quantities must be whole numbers of zero or more.
- Each priced variant has independent stock; CSV options share the product count.
- Stock edits reject stale quantities instead of overwriting completed sales.
- Variant fields are optional: leaving all three blank creates a product without priced variants.
- New-product options can be staged before the product has a database ID.
- Product creation and every staged variant are saved in one SaveChanges transaction.
- A completed editor row is included on Create/Update even when Add Variant was not clicked.
- Duplicate or invalid draft options reject the complete save without creating partial rows.
- An identical existing variant label for the same product is rejected.
- Missing products produce helpful feedback without recreating deleted records.

## Options and variants

Product Options lists labels separated by commas; these options share the base
product price. Product Variants assigns a separate price to each label. Priced
variants take priority in the storefront when both representations are present.
The form explains this distinction. Same-price labels are also shown on catalog
cards and remain selectable in Quick Add.

The variants editor is available during creation and editing. Add Variant stages
an option for a new product, or saves it immediately for an existing product.
Pending rows can be removed before creation; saved rows are displayed separately.
Leaving the option, unit price and stock quantity empty does not create a variant.

## Deletion policy

The page requests explicit browser confirmation before calling the deletion
operation. Cancellation leaves the database untouched. A confirmed deletion
removes the catalog product and its variants through the configured cascade.
The service does not delete physical image files.

Orders preserve purchased names, selected options, quantities, image paths and
unit prices in Orders.SerializedItems. Product edits and deletion do not rewrite
those snapshots or the order total. Reordering a removed product follows the
existing unavailable-product feedback in customer order history.

## Automated verification

ProductAdministrationServiceTests uses isolated in-memory SQLite databases and
real Identity role memberships. It covers unauthorized direct service calls,
revoked roles, security stamp changes, allowed-field creation, updates, invalid
input, variants, missing products and preserved order snapshots after deletion.
These tests do not validate browser dialog presentation.

## Manual verification

Use only the isolated development database and disposable products.
Record the commit, date, tester, actual result and Pass/Fail for these scenarios:

1. Create a product with a valid name, price, animal type and category.
2. Reload and verify the saved fields and image paths.
3. Edit the product, save and verify the changes after reloading.
4. Reject an empty name and a negative price without saving invalid changes.
5. Prepare multiple variants before creating a product and verify their saved prices.
6. Create a product with all variant fields blank and verify that no variant is created.
7. Reject an incomplete or duplicate variant without losing the other entered data.
8. Verify that a completed editor row is saved even without clicking Add Variant.
9. Check same-price Product Options on the catalog card and in Quick Add.
10. Add a valid variant to an existing product and reject an identical duplicate label.
11. Cancel the deletion prompt and verify that the product remains.
12. Place a test order before deleting the disposable product.
13. Confirm deletion and verify that the catalog product is removed.
14. Verify that the test order retains its original item name, option, quantity,
   unit price and total after editing and deleting the product.
15. As a customer and as an anonymous visitor, verify that /admin/products is denied.
16. Check the form and inventory controls on narrow and wide layouts and by keyboard.

The branch depends on the authorization changes for issue #24. The stock extension requires the AddProductStock schema migration. Peer review and local manual
verification are required before issue #13 is marked Done.
