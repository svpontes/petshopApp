# Pages

This folder contains the application's routable pages.  Its describing how the team can implement each page for PetShopApp.

The original implementation is a reference, not code to copy. Features below are planned work, not functionality already implemented in the new project. Agree on core requirements before treating every reference page as a required deliverable.

## Files and responsibilities

Paths are relative to `Components/Pages`. Routes are taken from the reference project; the team may revise them together.

### Storefront and shopping

| File | Reference route | Purpose and implementation guidance 
| --- | --- | --- |
`Home.razor` | `/` | Store landing page. The reference includes a rotating promotional banner, featured products, and product quick view. Build a responsive pet shop introduction with links to the catalog. Add featured products when the product service is available. 

`Catalog.razor` | `/catalog` | Displays the product grid with images, descriptions, prices, detail links, and quick view. Adapt it to pet products. Category/species filters and search are proposed additions, not features implemented in this reference page.

`ProductDetail.razor` | `/product/{id:int}` | Shows one product, its image gallery, description, price, quantity selection, cart action, and reviews.

`Cart.razor` | `/cart` | Shows selected products, quantities, line totals, and the cart total. Allows quantity changes and removal, with navigation to checkout and an empty-cart state. Delegate operations to the cart service and use the agreed variant model. 

`Checkout.razor` | `/checkout` | Collects customer and delivery details, displays the order summary, and submits an order. The reference saves an order and clears the cart; it does not implement a payment gateway. Agree whether the course project uses simulated checkout or a payment provider's test environment. Show success only after the order is saved. 

`OrderSuccess.razor` | `/order-success` | Dedicated order confirmation page with links to continue shopping or return home. The reference uses a random display-only order number and static text claiming an email was sent. Use the actual saved order ID and show only actions the application really performs. Coordinate navigation with Checkout, which also contains its own success view in the reference. 

`Contact.razor` | `/contact` | Shows store contact information and a customer inquiry form. The reference saves messages for administrators to read. Implement field validation and clear success/error feedback through a contact service. 

### Customer accounts

| File | Reference route | Purpose and implementation guidance |
| --- | --- | --- |

`Login.razor` | `/login` | Customer and administrator sign-in form. The reference calls `AuthService` and redirects according to the user's role. Implement it against the authentication approach agreed by the team, with validation and failure feedback.

`Register.razor` | `/register` | Creates a customer account. The reference validates input, checks for an existing email, saves the user, and signs them in. Use the agreed authentication system and password hashing rather than copying the reference's password handling.

`ForgotPassword.razor` | `/forgot-password` | Starts account recovery. The reference checks an email and displays feedback, but does not send recovery email or reset a password. Implement a real token-based flow if in scope, or clearly identify the feature as a demonstration. 

`Profile.razor` | `/profile` | Primary customer account page. Displays and edits contact/delivery details and presents order history. Use authenticated-user data and ensure customers can access only their own account and orders.

`UserProfile.razor` | `/profile-backup` | Alternate profile implementation retained as a backup in the reference. It also supports profile editing and order history. It is not a separate customer feature. Keep an empty placeholder only if preserving the reference file inventory.

### Administration

All administrative data access and operations must enforce administrator authorization. Hiding links or redirecting the page alone is not sufficient protection.

| File | Reference route | Purpose and implementation guidance |
| --- | --- | --- |

`AdminDashboard.razor` | `/admin` | Administration overview with order, message, subscriber, and revenue totals and links to management pages. Load summary data through services. Distinguish order totals from actual paid revenue if payments are simulated. 

`AdminProducts.razor` | `/admin/products` | Product management: list, create, edit, and delete products. Adapt fields to the agreed pet product model, including categories and any optional variants. Validate data and coordinate changes with the model/database owner. 

`AdminOrders.razor` | `/admin/orders` | Displays orders, customer/delivery information, purchased items, and totals. The reference reads item summaries stored as JSON and does not implement order-status editing. Use the team's agreed order representation; status management would be additional work. 

`AdminMessages.razor` | `/admin/messages` | Read-only view of submitted contact messages, including sender, subject, date, and content. The reference does not implement replies or message-status management. Add those only if agreed in scope. 

`AdminSubscriber.razor` | `/admin/subscribers` | Lists newsletter subscribers and supports deletion. Coordinate with the footer subscription form and the subscriber model/service. This does not send newsletter campaigns. Preserve the reference's singular filename if maintaining the same structure. 

### Error handling

| File | Reference route | Purpose and implementation guidance |
| --- | --- | --- |

`Error.razor` | `/Error` | Displays an application error message and request identifier for troubleshooting. Preserve the working template page initially and adapt its presentation later. Keep technical exception details out of production responses.

`NotFound.razor` | `/not-found` | Displays a friendly message when a requested page cannot be found. Preserve the working template page initially; later add useful navigation back to the home page or catalog. Coordinate with routing and status-code handling. 

### Store policies — `legal/` subfolder

Keep the lowercase `legal` subfolder to match the reference structure. The URL is determined by `@page`, not the folder name.

| File | Reference route | Purpose and implementation guidance |
| --- | --- | --- |

`legal/Shipping.razor` | `/shipping-policy` | Explains delivery areas, shipping charges, estimated times, and tracking where supported. Write content that matches the pet shop's agreed behavior. 

`legal/Returns.razor` | `/returns-exchanges` | Explains how customers request returns or exchanges and which conditions apply. A

`legal/Privacy-policy.razor` | `/privacy-policy` | Explains which personal data the application processes, its purposes, and relevant contact information. Align it with the features actually implemented. Keep the reference filename if preserving the inventory. 

`legal/Cookies.razor` | `/cookies` | Explains the cookies and similar storage mechanisms actually used. Any consent controls are a separate implementation task; this page alone does not provide them.

## Creating empty files safely

1. Keep the current working `Home.razor`, `Error.razor`, and `NotFound.razor` generated by the template. Do not overwrite them with empty files.
2. Create the other files as empty placeholders when the team is ready to assign them. Create `legal/` for the four policy pages. This README does not create those files.
3. Empty `.razor` files do not create navigable pages. Add the agreed `@page` directive and a simple heading when implementation starts.
4. Add menu/footer links only when the destination page has a route.
5. Do not inject services or reference models until those dependencies exist and are registered where necessary.
6. Avoid duplicate routes, especially when working with `Profile.razor` and the optional backup page.

Example starting point for `Catalog.razor` when its implementation begins:

```razor
@page "/catalog"

<PageTitle>Catalog - PetShopApp</PageTitle>

<h1>Pet Products</h1>

@* TODO: Implement the product catalog according to the team's agreed requirements. *@
```

## Shared development rules

- Pages handle presentation, user input, and calls to application services. Put business operations in `Services`, domain classes in `Model`, and database configuration in `Data`.
- Reuse components from `Components/Shared` rather than duplicating product cards, ratings, or filters across pages.
- Agree on model properties and service interfaces before connecting independently developed pages.
- Include loading, empty, validation, and failure states where applicable.
- Work on a task branch, check that the application builds, and submit a pull request for review.

## Suggested implementation order

1. Agree on product/category models and service contracts; build Home, Catalog, and ProductDetail.
2. Implement Cart, Checkout, and order confirmation against the agreed order model.
3. Add account features and the required administration pages, coordinating authorization and persistence.
4. Complete contact/policy pages and any agreed enhancements, then test the complete shopping flow.

This sequence is a planning guide. The team can develop independent tasks in parallel once shared contracts are agreed.
