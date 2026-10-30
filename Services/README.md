# Services

This folder contains application logic and state used by pages and reusable components.
The descriptions distinguish reference behavior from proposed PetShopApp improvements.

## Files and responsibilities

| File | Purpose in the reference project | Guidance for PetShopApp |

`AuthService.cs` | Provides a custom in-memory representation of the current user. Queries the database during login, exposes login/admin state, clears the current user on logout, and notifies components when state changes. | Coordinate the authentication design with the user model and database owner. Use the agreed ASP.NET Core authentication/authorization system and password hashing instead of copying the reference's plaintext-password comparison. 

`CartService.cs` | Keeps a cart in memory, adds/removes products, changes quantities, calculates totals, and notifies subscribed components. It also defines the `CartItem` class in the same file. | Agree on cart identity and optional product variants. Keep cart operations reusable across product pages, quick view, navigation, and checkout. 

## AuthService.cs

### Reference members

| Member | Responsibility |

Constructor receiving `IServiceScopeFactory` | Allows login to create a temporary dependency-injection scope and resolve `ApplicationDbContext` for its database query. 

`CurrentUser` | Holds the current `User`, or `null` when no user is signed in. The reference exposes a public setter. 

`IsLoggedIn` | Returns whether `CurrentUser` is present. 

`IsAdmin` | Checks whether the current user's role string equals `Admin`. 

`Login(email, password)` | Queries users, includes order history, and compares email and password directly. On success, stores the user, raises `OnChange`, and returns `true`; on failure, returns `false`. 

`Logout()` | Clears `CurrentUser` and raises `OnChange`. 

`OnChange` | Lets subscribed components update their display when the user logs in or out. 

`NotifyStateChanged()` | Private helper that invokes `OnChange`.

### Implementation boundaries

The reference does not implement ASP.NET Core authentication cookies, a claims principal, secure password verification, registration, or password-reset email. Its role helper is not a substitute for enforcing authorization on protected operations.

For the new project, agree on the authentication approach before implementing Login, Register, Profile, and administration. Avoid allowing arbitrary components to modify trusted user/role state. Fetch a customer's orders through authorized application operations rather than treating cached order history as permanently current.

## CartService.cs

### Reference members

| Member | Responsibility |

`_cartItems` | Internal list storing cart entries. 

`CartItems` | Exposes the actual mutable list in the reference. Consider a read-only view so changes go through service methods and notifications remain consistent.

`TotalItems` | Sums item quantities; it does not count only distinct product lines.

`TotalPrice` | Sums each product's price multiplied by its quantity. It does not include shipping, tax, discounts, or payment processing. 

`AddToCart(product, size, quantity)` | Ignores null products and nonpositive quantities. Combines entries with the same product ID and size, or creates a new entry. 

`UpdateQuantity(productId, size, change)` | Adds a quantity difference, such as `+1` or `-1`, to a matching entry. Removes it when the resulting quantity is zero or less. 

`RemoveFromCart(productId, size)` | Removes a matching product-and-size entry. 

`OnChange` | Notifies subscribed UI components after cart changes. 

`NotifyStateChanged()` | Private helper that invokes `OnChange`. 

### CartItem class

`CartItem` is defined inside `CartService.cs` in the reference, in the services namespace. It contains:

- `Product`: the selected product object.
- `Size`: the selected size, defaulting to `size` in the reference.
- `Quantity`: the number of units selected.

For PetShopApp, replace the clothing-specific assumption with an agreed optional variant, such as package weight or accessory size. Products without variants must work too. If the team moves `CartItem` into `Model/CartItem.cs`, update its namespace and all consumers together; this is a proposed change, not the reference file structure.

### State and persistence

The reference cart is an in-memory list. It does not persist to SQLite, browser storage, or a customer account. With scoped registration in interactive Blazor Server, its state normally belongs to the user's circuit and can be lost when a new circuit is created, including after a full reload. Do not promise a permanent or cross-device cart without implementing persistence.

Cart prices are taken from the held product objects. Checkout must revalidate current prices, product availability, and quantities using trusted application data before saving an order. Stock checks and purchase-time snapshots are not implemented by this service.

## Integration with the rest of the project

- ProductDetail and product quick view call the cart service to add selections.
- Cart displays entries and calls quantity/removal methods.
- NavMenu displays cart totals and account state.
- Checkout uses cart contents to request order creation; clear the cart only after successful order creation.
- Login calls the chosen authentication implementation; account and administration pages use its authenticated identity and authorization rules.
- Components that subscribe to service events must unsubscribe when disposed.

Register implemented services through dependency injection in `Program.cs`. The reference uses scoped registration for both services; do not register user-specific cart or account state as a singleton shared by all users. Do not register or inject placeholder classes before they exist.

## Proposed additional services

The following are possible additions, not files present in the reference Services folder:

| Proposed service | Responsibility |

`ProductService.cs` | Product queries and administration operations used by catalog and product pages.
`OrderService.cs` | Validates checkout inputs and current product data, calculates order totals, and saves orders. 
`ContactService.cs` | Validates and stores customer inquiries. 
`NewsletterService.cs` | Handles subscription rules and persistence. Email delivery would require separate integration. 

Add only the services needed for agreed requirements. For database operations in interactive Blazor Server, coordinate short-lived EF Core contexts with the database owner, preferably through a context factory per operation.

## Team implementation workflow

1. Agree on model properties, variant identity, service methods, and expected outcomes before pages depend on them.
2. Coordinate persistence and schema changes with Sergio, who is responsible for the database and migrations.
3. Empty `AuthService.cs` and `CartService.cs` placeholders may be created first. They define no usable types until implemented.
4. Implement and test cart operations, including merging entries, quantity updates, removal, and totals.
5. Implement the agreed authentication approach and enforce authorization on protected operations.
6. Connect pages and shared components gradually, checking that the project still builds.
7. Work on task branches and submit changes for review before integration.
