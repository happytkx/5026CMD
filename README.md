# 5026CMD — Restaurant Reservation and Order Management System

RROMS (Céleste) is a web-based restaurant system for customer reservations, dine-in ordering, staff operations, billing, and administrator management. This guide explains how to run and assess the current application locally.

## 1. Requirements

- Windows with the **.NET 10 SDK**.
- **SQL Server Express LocalDB**, with an instance named `MSSQLLocalDB`.
- A browser and internet access for the first package restore.
- Optional: Visual Studio with ASP.NET/web development support and .NET 10 support.

The application uses ASP.NET Core Razor Pages, Entity Framework Core, and SQL Server. The default database is `RROMSDb` on `(localdb)\MSSQLLocalDB`.

## 2. Start the application

Open a terminal in the repository folder and run:

```powershell
dotnet --version
dotnet restore RROMS/RROMS.csproj
dotnet dev-certs https --trust
dotnet run --project RROMS/RROMS.csproj --launch-profile https
```

Accept the certificate trust prompt if shown, then open **https://localhost:7096**. Keep the terminal running; press `Ctrl+C` to stop the application.

Use the **HTTPS** address for testing: authentication cookies require a secure connection. In Visual Studio, open the project, select the `https` launch profile, and run it.

On startup, the application automatically applies database migrations and creates initial data. A separate SQL import or manual migration command is not required. The launch profile sets the environment to `Development`, and `RROMS/appsettings.Development.json` enables fictional demonstration data.

Existing data is retained between runs. Demo accounts are created when missing; restarting does not reset a password that has already been changed. Calendar fixtures may be refreshed by the development seeder, so use the dates and statuses currently displayed in the application.

## 3. Test accounts

These are built-in development credentials for local assessment only. They are not production accounts.

| Role | Email | Password | Login page |
| --- | --- | --- | --- |
| Customer | `customer1@celeste.example.test` | `CelesteGuest@2026!` | `/Account/Login` |
| Second customer | `customer2@celeste.example.test` | `CelesteGuest@2026!` | `/Account/Login` |
| Restaurant staff | `staff@celeste.local` | `Staff123!` | `/Account/StaffLogin` |
| Administrator | `admin@celeste.local` | `Admin123!` | `/Account/StaffLogin` |

Append the login path to `https://localhost:7096`. Customers 3 and 4 are also available as `customer3@celeste.example.test` and `customer4@celeste.example.test`, using the same customer password. Customer demo credentials can be overridden through `DemoData:CustomerPassword` before accounts are first created.

Use separate browser profiles or different browsers for customer and staff testing. Otherwise, signing in to another role replaces the current login in that browser session.

## 4. Suggested assessment walkthrough

Before testing, sign in as administrator and inspect **Restaurant Policies** (`/Admin/RestaurantPolicies`). The examples below assume the default policy values in section 5. If policies were edited, use the amounts and terms shown on the individual reservation instead. Restarting does not restore default policies.

### A. Customer reservation

1. Sign in as a customer and open Reservations.
2. Create a reservation for **2 people, Indoor**, using an available slot tomorrow or later.
3. Confirm the booking details and the deposit: **RM100 at the default RM50-per-person policy**.
4. Complete a successful **simulated** deposit payment before the displayed hold deadline. Expect the reservation to become `Confirmed` and a receipt to be available.
5. In a separate booking, try a failed payment and check that it does not confirm the booking. Retry before the hold expires.

The payment interface is a simulation; it does not charge a real card or transfer money.

### B. Seating and dine-in ordering

1. Sign in as staff and open the seating/reservation screens.
2. Use a confirmed reservation whose booked arrival time has been reached, and check in within its arrival window. Expect the status to become `Seated`.
3. Sign in as that reservation's customer, browse the menu, add dishes, and submit the order.
4. As staff, open Orders and progress dishes through `Received`, `Preparing`, `Ready to Serve`, and `Served`.
5. Check the customer order view and notifications for the updated progress.

**Ordering is available only for seated reservations.** A future confirmed reservation cannot be used immediately for ordering. For a quicker demonstration, inspect the seeded bookings for a currently seated reservation and use its associated customer account. Seeded availability depends on the current time and existing database state; if none is suitable, test at a valid booking time rather than changing the computer clock.

### C. Billing and payment

1. Use a seated reservation with a paid deposit and active dishes that have all been served.
2. Ensure the food subtotal meets the reservation's saved minimum spend. For a 2-person Indoor booking created under the default policy, the minimum is **RM100**, excluding tax.
3. Open staff Billing and generate the bill from the actual orders.
4. Check the food subtotal, tax, total, deposit applied, and remaining balance.
5. Record final payment using a supported option displayed by the application, then inspect the payment receipt and billing history.
6. Try billing with unfinished dishes or insufficient food spending and verify that settlement is blocked with an explanation.

### D. Administrator features

Sign in as administrator and test:

- Menu items, categories, availability, and dated promotions.
- Dining areas, tables, restaurant hours, and reservation slot interval.
- Customer and staff account creation/editing, individual activation/deactivation, and bulk deactivation. Bulk activation is not currently provided.
- Restaurant Policies: deposit, minimum spending, advance notice, rescheduling, arrival grace, dining duration, payment hold, and general guidelines.
- Announcements, scheduling, pinning, and homepage display.
- Customer reviews, payment collections, occupancy, and reporting screens.

Make a small identifiable change (for example, a test announcement) and verify its effect in the customer view. Remove or reverse the change after assessment if desired.

To test policy changes, record the original settings and create a reservation before editing. Change the deposit per guest, then create another reservation with the same party size. Verify that the new reservation uses the new deposit while the earlier reservation retains its saved amount. Restore the original setting afterwards; restoring it does not rewrite either reservation. Existing bookings also retain their saved minimum spending, rescheduling limits, arrival grace, dining duration, and advance-notice terms.

### E. Access control and reviews

- Confirm that customers see their own reservations and orders, not another customer's records.
- Confirm that staff cannot access administrator-only management pages.
- Confirm that administrators can use both staff operations and administrator features.
- Submit a customer review and verify it appears in the review views. Reviews may be general or linked to a completed dining order.

Administrators follow the same reservation, deposit, cancellation, modification, and billing validation as staff; their role does not bypass business rules.

## 5. Rules that affect test results

All reservation policy times use **Malaysia time (UTC+8)**. Administrators can edit the policy values identified below. Defaults describe initial values, not fixed limits. Each reservation saves its agreed amounts and relevant policy terms when created.

| Rule | Expected behaviour |
| --- | --- |
| Party size | 1–5: Indoor/Outdoor; 6–8: Indoor/Outdoor/VIP Room; 9–12: VIP Room |
| Advance booking | Configurable; defaults to at least 2 hours for Indoor/Outdoor and 24 hours for VIP Room |
| Deposit | Configurable; defaults to RM50 per person; the amount is saved on the reservation |
| Payment hold | Configurable; defaults to up to 10 minutes, shortened if the advance-booking cutoff is reached sooner |
| Minimum food spend | Configurable; defaults to Indoor/Outdoor RM50 per person, VIP 6–8 guests RM2,400, VIP 9–12 guests RM3,600 |
| Dining duration | Indoor/Outdoor duration is configurable (default 2 hours); VIP occupancy ends at closing time |
| Latest arrival slot | All areas must leave the configured standard dining duration before closing, including VIP Room |
| Modification | Configurable; defaults to once, more than 168 hours before the **original** booking time; existing bookings use their saved limits |
| Cancellation | The full deposit is forfeited |
| Arrival window | Configurable; defaults to 10 minutes from the booked time, extended to 30 minutes when late arrival is notified; existing bookings use their saved terms |
| Ordering | Requires reservation status `Seated` |
| Final settlement | Requires served active dishes and the minimum food spend; tax does not count towards that minimum |

Party-size eligibility, cancellation forfeiture, seated-only ordering, and settlement checks remain enforced. Restaurant Settings separately controls opening/closing hours and reservation intervals of 15, 30, or 60 minutes, with checks that protect existing bookings.

With default policies, test modification using a paid booking more than 7 days ahead, such as 10 days ahead. If policies differ, use a booking outside its saved modification cutoff and within its allowed modification count. Use a separate booking to test cancellation.

## 6. Email and registration

Email delivery is **disabled by default** (`Email:Enabled` is `false`). Use the pre-confirmed demo customers for the main assessment without configuring email.

New-account email verification, password-reset messages, and reservation emails require a working SMTP configuration. Messages to fictional `.test` addresses are deliberately blocked. For email assessment, use a real test inbox and configure `Email:Enabled`, `Host`, `Port`, `SslOnConnect`, `SenderAddress`, `Username`, and `Password` through local configuration or .NET user secrets. Do not put SMTP passwords in committed files.

## 7. Optional automated checks

Run these from the repository folder. These projects are console verification runners, so use `dotnet run`:

```powershell
dotnet run --project tests/RROMS.Sprint2.Tests/RROMS.Sprint2.Tests.csproj
dotnet run --project tests/RROMS.Sprint4.BillingChecks/RROMS.Sprint4.BillingChecks.csproj
```

For the reservation database integration checks (requires LocalDB):

```powershell
dotnet run --project tests/RROMS.Sprint2.Tests/RROMS.Sprint2.Tests.csproj -- --integration
```

The integration runner creates an isolated temporary SQL database and uses a fake email sender. Check the terminal for passing results or failure details. These checks supplement the browser walkthrough. Passing the default-policy checks does not verify all editable-policy combinations; also perform the before/after policy test in section 4.

## 8. Troubleshooting

| Problem | What to check |
| --- | --- |
| .NET target framework is unsupported | Install the .NET 10 SDK and verify `dotnet --version`. |
| Database connection fails | Confirm LocalDB is installed. Run `SqlLocalDB info` and `SqlLocalDB start MSSQLLocalDB`; then restart the app. |
| HTTPS certificate warning | Run `dotnet dev-certs https --trust`, accept the prompt, and reopen the browser. |
| Login does not persist | Use `https://localhost:7096`, not the HTTP address. |
| Demo account is missing | Run with the `https` Development profile and confirm `DemoData:Enabled` is `true` for customer fixtures. |
| Password is rejected | Use the correct customer/internal login page. Existing passwords are not reset by restarting. |
| No available booking slots | Check party size, dining area, advance notice, opening hours, and existing bookings. |
| Cannot check in or order | Check the booked time and arrival window; the reservation must be seated before ordering. |
| Cannot settle a bill | Serve all active dishes and meet the food minimum before recording payment. |
| Amounts or deadlines differ from this guide | Check Restaurant Policies and the reservation's saved terms. The examples use defaults; existing bookings retain their agreed terms after policy edits. |
| No verification/reset email | Email is disabled by default; SMTP and a real recipient address are required. |
| Too many login attempts | Wait for the rate-limit window to clear; repeated failed passwords can also lock the account temporarily. |

If using another SQL Server instance, configure `ConnectionStrings:DefaultConnection` for that server. The supplied LocalDB setup is Windows-specific.
