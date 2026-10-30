# Data

This folder contains database access configuration and initialization code for PetShopApp.

## ApplicationDbContext.cs

`ApplicationDbContext` is the application's Entity Framework Core database context. It connects the application's model classes to the configured database, provides access to stored records, tracks entity changes, and saves those changes when `SaveChanges()` or `SaveChangesAsync()` is called.

The supplied design inherits from `DbContext` and uses model classes in the `PetShopApp.Models` namespace. The source folder is named `Model`; folder names and C# namespaces do not have to be identical, but namespace declarations and references must be consistent.

This document describes the intended context. It does not mean the models, packages, or database have already been implemented in the new project.

## Constructor and configuration

The constructor receives `DbContextOptions` and passes them to the base `DbContext` class. These options specify settings such as the database provider and connection string.

Configure and register the context in `Program.cs` through dependency injection. Keep the connection string in application configuration. For the planned SQLite setup, the team must agree on the database filename and location.

The supplied constructor uses non-generic `DbContextOptions`, which EF Core supports. When implementing the context, prefer `DbContextOptions<ApplicationDbContext>` to associate its configuration explicitly with this context type.

## Database sets and responsibilities

A `DbSet<T>` exposes entities that can be queried, added, updated, or removed. In this relational design, these sets are intended to map to database tables; model configuration and migrations determine the actual schema.

| Property | Model | Purpose |

`ContactMessages` | `ContactMessage` | Stores inquiries submitted through the contact form for administrators to review. A complete support-ticket workflow would require additional fields and logic. 

`Orders` | `Order` | Stores customer orders and their purchase details. The reference project includes serialized item data; the team must agree on whether to use that representation or related order-item records. 

`Users` | `User` | Stores account and profile records in the supplied design. Coordinate this model with the chosen authentication system. Password protection is not provided by declaring this set; never store plaintext passwords. 

`Subscribers` | `NewsletterSubscriber` | Stores newsletter subscription records. Saving a subscriber does not send emails or implement a newsletter campaign. 

`ProductReviews` | `ProductReview` | Stores customer ratings and written product reviews. Product/user relationships and validation must be defined separately. 

`Products` | `Product` | Stores product information, such as names, descriptions, prices, and images. Categories, stock, and optional variants require corresponding model properties and configuration. 


The supplied context has no explicit category or order-item set. Agree on those models and relationships before extending the schema. Related entities can also be discovered through navigation properties; not every table requires its own `DbSet` property.

## How the context fits into the application

1. Classes in `Model` define the data structures and relationships.
2. `ApplicationDbContext` exposes those entities to EF Core.
3. `Program.cs` registers the context and configures the database provider.
4. Application services use the context to query and save records.
5. Pages call services and display the results.
6. Migrations in `Migrations` describe schema changes and are applied to create or update the database.

Adding a `DbSet` or editing a model does not automatically update an existing database. Generate, review, and apply the appropriate migration after configuring EF Core and its tooling.

## Implementation checklist

- Agree on the models and shared property names with the team.
- Install the agreed EF Core SQLite and design packages, and configure the local EF tool.
- Create the model classes referenced by the context before adding references to them.
- Implement `ApplicationDbContext` and register database access in `Program.cs`.
- Configure and verify the SQLite connection string.
- Generate and review the initial migration, then apply it to the development database.
- Verify that products and orders can be saved and retrieved through application services.

An empty `ApplicationDbContext.cs` placeholder is acceptable initially. Do not paste the complete context until its model and package dependencies are available, or the project will fail to compile.

## Team coordination

- Coordinate schema changes with the participant responsible for data and migrations.
- Keep pricing rules, checkout decisions, and other business operations in `Services` rather than in this context.
- Review migrations before merging; do not delete or rewrite migrations already shared and applied without coordinating with the team.
- Keep production data, credentials, and personal information out of the repository. Use agreed demonstration data for development.
- For interactive Blazor Server components, do not share one long-lived context across concurrent operations. Prefer an EF Core context factory and a short-lived context per operation when implementing database services.
