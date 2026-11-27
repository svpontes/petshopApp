# Local Identity accounts and authorization

## Account model

PetShopApp uses ASP.NET Core Identity with integer user IDs, SQLite persistence,
framework password hashing, application cookies, and the Customer and Admin roles.
Identity memberships in AspNetUserRoles are authoritative. Editing the legacy
Users.Role column is not an administrator setup procedure.

Register customer accounts through /register. The HTTP registration endpoint assigns
Customer itself and ignores browser-supplied roles. Login and logout use HTTP POSTs
with antiforgery validation so the browser receives the cookie changes.

The application cookie is HttpOnly, uses SameSite=Lax, and expires after one hour
with sliding renewal. Production requires secure cookies. Browser refresh restores
Identity state; closing the browser is not a guaranteed session-expiration mechanism.
Active circuits are revalidated approximately every minute. Protected operations also
call AuthService.IsAuthorizedAsync to read current account/stamp/role data before acting.

## Prepare an isolated local database

Use the repository's global.json SDK. In PowerShell at the repository root:

```powershell
# Select development settings and an isolated database for this task.
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
$env:ConnectionStrings__PetShopDatabase = "Data Source=E:\petshop-team\petshop-identity-authorization-test.db"

# Restore and start the application without launch-profile overrides.
dotnet restore .\PetShopApp.slnx
dotnet run --project .\PetShopApp.csproj --no-launch-profile --urls "http://localhost:5177"
```

Normal startup applies migrations, seeds the demonstration catalog when needed, and
initializes Identity roles. No new migration is required by this authorization change.
Register at least two independent customer accounts for privacy checks.
Never commit local database files, credentials, or personal account data.

## Grant local development administrator access

1. Register a dedicated local account through /register, for example admin-local@example.test.
2. Stop the application with Ctrl+C. Keep the same development environment and database.
3. Run the explicit command below. It updates the existing account and exits without starting a server.

```powershell
# Grant the role to the dedicated account already registered in the test database.
dotnet run --project .\PetShopApp.csproj --no-launch-profile -- --grant-development-admin "admin-local@example.test"
```

4. Start the application normally and sign in again. Existing claims are invalidated by a
   security stamp change; the new sign-in obtains the Admin role.
5. Verify /admin and /admin/products using that account.

The command refuses Production and Staging, rejects an unregistered address, preserves
the password hash and account ID, and does nothing if the account is already an admin.
It does not run on normal startup and has no browser endpoint. Do not use this development
procedure to administer a deployed application or point it at a production database.

## Authorization review

| Area | Enforcement |
| --- | --- |
| Administrative routes | Authorize(Roles = "Admin") on the routed pages |
| Administrative reads and mutations | Fresh IsAuthorizedAsync(requireAdmin: true) checks at each operation |
| Customer profile and confirmation | Authorize plus fresh account checks before protected operations |
| Order creation and history services | Fresh account check inside the service; trusted customer ID |
| Current and backup order history | Order.UserId identifies the owner; customer-entered email is not an access key |
| Public registration | Server assigns Customer through UserManager; no role input is trusted |
| Role or stamp changes | Fresh operation check and framework cookie/circuit revalidation |

Future product/category/order administration services must perform their own authorization
checks before data access or mutation. Keep the checks inside the service when moving
operations out of pages in issues #13, #14 and #23.

## Automated checks

```powershell
# Build the application and tests, then execute the suite.
dotnet build .\PetShopApp.slnx
dotnet test .\PetShopApp.slnx --no-build --no-restore
```

IdentityAuthorizationTests uses a separate in-memory SQLite database per test. It covers
anonymous/customer denial, direct unauthorized service calls, forged role claims,
revoked admin membership, changed security stamps, deleted accounts, development-only
administrator setup, preserved credentials, repeat execution, and missing accounts.
These tests exercise authorization and role setup, not HTTP cookie issuance or browser layout.
The existing CartServiceTests remain part of the same suite.

## Manual acceptance record

Record the tested commit, date, tester, expected result, actual result and Pass/Fail.
Do not record passwords or cookies. The following scenarios still require local browser verification:

- Register customer A, refresh, and confirm that the session is restored.
- As an anonymous visitor, try /profile, /checkout, /order-success/1 and /profile-backup.
- As a customer, try /admin, /admin/products, /admin/orders, /admin/messages and /admin/subscribers.
  Use the exact subscriber route declared by the application if it differs.
- Sign in with the dedicated administrator and verify the allowed pages and operations.
- Place an order for A with B's email in the checkout contact field. Only A owns that order.
  Verify both /profile and /profile-backup as A and as B.
- Log out A and log in B; verify that A's cart items and orders are not exposed to B.
- Verify logout removes authenticated access after refreshing and after using browser navigation.
- Review relevant account relationship and migration decisions with Rebecca, and obtain peer review.

Keep issue #24 In Progress until the documented local checks, database coordination and
peer review are complete. Password confirmation, category administration and order status
management are separate backlog tasks.

## Framework references

- https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0
- https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-10.0

## Verification — 2026-11-27

- Tester: Sergio Pontes.
- Application and test projects build successfully.
- Automated tests: 13 passed, 0 failed.
- Manual scenarios AUTH-01 through AUTH-09: all passed.
- Verified session persistence, protected access, administrator access,
  logout, cart isolation, and order ownership.
- Tests used an isolated local database.
- Pull request review is pending.