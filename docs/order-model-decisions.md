# Order Models – Sprint 1 Foundation (#18)

## Data model

- `Order` stores its ID, creation date, total amount, current status, customer contact, shipping address and `UserId`.
- `OrderItem` records the purchased product ID, product name, image, selected size/variant, quantity and purchase-time unit price.
- Order items are serialized into `Order.SerializedItems` (JSON). They remain an order snapshot even if catalog names and prices change later.
- Products without variants are supported: `OrderItem.Size` may be empty.
- The existing SQLite schema already has a nullable `Orders.UserId` foreign-key column; the explicit model property makes it accessible to services. There is no schema change or migration in this PR.

## Agreed business rules

- Checkout requires an authenticated customer; guest checkout is not supported for the MVP.
- The order's `UserId` is assigned from the logged-in session, never from the form.
- Only the owner may view a customer order; administrative access is restricted to the `Admin` role.
- Order statuses: `Processing` (initial), `Shipped`, `Delivered`, `Cancelled`. Exact transition validation belongs to order administration, not these models.
- Product stock/inventory deduction is outside the MVP scope.
- Database persistence and migrations remain Rebecca's separate issue (#19).
- Order creation, validation, checkout, and administrative features are handled in later issues (#20–#23).

## Review checklist

- [ ] Rebecca: confirm existing database mapping and purchase snapshots
- [ ] Sergio: confirm logged-in customer association and guest-checkout decision
- [ ] Build passes with current .NET SDK
