# Model

This folder defines the application's data structures. 

## Files and responsibilities

| File | Purpose in the reference project | Guidance for PetShopApp |

`Product.cs` | Represents a catalog product: ID, name, description, price, primary image, secondary images, and available sizes. Secondary image URLs are stored in a newline-separated string and exposed through a helper list. | Agree on the core product fields first. Add category, stock, species, or optional variants only as required by the agreed scope. Define how images and variants will be persisted. 

`Order.cs` | Represents an order with date, total, status, customer name/email, shipping address, and purchased-item data stored as JSON in `SerializedItems`. Its `OrderItems` helper converts between JSON and a list. 
Define the order lifecycle and customer association. Decide whether items will remain serialized or become related records in an order-items table. Creating an order does not by itself mean a payment has been collected. 

`OrderItem.cs` | Represents a purchased item with product ID, product name, image, purchase price, quantity, and selected size. It preserves product information as it was at purchase time. In the reference order model, these objects are serialized rather than mapped through the `OrderItems` helper. | Preserve purchase-time prices and descriptions so catalog changes do not rewrite order history. Adapt size to the agreed optional variant model. If using related database records, define the order foreign key and relationship explicitly. 

`User.cs` | Contains account identity, name, email, password, avatar, role, optional contact/address fields, creation date, and an `OrderHistory` collection. | Coordinate with the authentication owner before implementing this model. The reference stores plaintext passwords; do not reproduce that design. Use the chosen authentication system and password hashing. Confirm how orders are associated with users instead of relying only on email matching. 

`ProductReview.cs` | Stores a product review: ID, product ID, reviewer email/name, rating, comment, and creation date. | Agree on rating limits, required fields, and product/user relationships. Associate reviews with the authenticated user. Decide whether reviews are a core feature or an enhancement. A `ProductId` property alone does not guarantee a database foreign-key constraint without relationship configuration. 

`ContactMessage.cs` | Stores a contact form submission: ID, sender name/email, subject, message, and creation date. It has no namespace declaration in the reference. | Place the new class in `PetShopApp.Models` for consistency. Agree on validation and which fields administrators can view. This model records inquiries; a ticket/reply workflow would require additional design. 

`NewsLetterSubscriber.cs` | Defines the class `NewsletterSubscriber`, containing an ID, email, and subscription date. The filename has different capitalization from the class name. | Preserve the reference filename if matching its inventory, but use the exact class name `NewsletterSubscriber` in code. Coordinate duplicate-email handling and subscription rules. This record does not verify email ownership or send newsletters. 

`BreadcrumbItem.cs` | UI data model containing `Text`, `Url`, and `IsActive` for a breadcrumb navigation step. | Use it with `Components/Layout/Breadcrumbs.razor`. It normally remains a presentation model and should not be included as a database entity. 

`MockData.cs` | Static helper whose `GetMockProducts()` method returns six sample clothing products in memory. | Replace these examples with fictional pet products if the team needs temporary UI data. Calling this helper does not insert data into SQLite. Keep database seeding in a separate initialization process under `Data`. 

## Important persistence details

- A class in this folder is not automatically a database table. EF Core discovers entities through the context, relationships, and explicit configuration.
- In the reference `Product`, `AvailableSizes` is marked `[NotMapped]` and is not persisted by EF Core. `SecondaryImageUrls` is also `[NotMapped]`, but its helper writes to the mapped `SecondaryImageUrlsRaw` string.
- In the reference `Order`, `OrderItems` is `[NotMapped]`; the JSON string `SerializedItems` is persisted instead.
- SQLite can store related records in separate tables. Serializing images or order items is a design choice, not a requirement imposed by SQLite.
- Data annotations, Fluent API configuration, and migrations must reflect the agreed validation rules and relationships.

## Proposed additions — not in the reference folder

`Category.cs` is a proposed new model for organizing pet products. The reference `Product.cs` does not contain a category property. Agree on category fields and its relationship with products before implementation.

A separate product-variant model may also be useful if the scope includes different package weights or accessory sizes. It is not required merely to preserve the original folder structure.

## Team workflow

1. Sergio coordinates the database design and migrations with the team.
2. Agree on model names, property types, required fields, relationships, and service contracts before pages depend on them.
3. Start with `Product` and the proposed `Category`; then agree on `Order` and `OrderItem` for the shopping flow.
4. Implement the models needed for the current sprint. Empty `.cs` placeholders are acceptable, but they do not define types that other code can reference.
5. Update `ApplicationDbContext` and generate, review, and apply migrations for schema changes.
6. Notify the other contributors when a shared model changes; commit the relevant model, context, and migration files together.

## Naming and responsibility rules

- Keep the folder name `Model` to match the reference structure.
- Use the namespace `PetShopApp.Models` consistently in the new classes and imports. The folder name and namespace do not have to be identical.
- Keep checkout decisions, cart operations, and other application workflows in `Services` rather than in model classes.
- Do not add database sets for `BreadcrumbItem` or `MockData`.
- Use fictional development data; do not carry over real customer records or credentials from the previous project.