# Sprint 2 Reservation Management

Implemented for Wong Jia Ying: US11–US20 and US47–US52. The scope is reservation management. Menu ordering, final billing, reports, account administration and production login remain with the other sprints.

## Start and preview

Open `RROMS.slnx` in Visual Studio and run the RROMS web project. Configure `ConnectionStrings:DefaultConnection` for your SQL Server database. Startup applies EF migrations and seeds initial data if the tables are empty. Back up an existing database before its first upgrade.

During Development, `/Access` provides clearly labelled customer/staff previews when `Sprint2:EnableDemoAccess` is true in `appsettings.Development.json`. This preview is unavailable in Production. It is a testing bridge, not account registration or password authentication. Existing seeded customer passwords are not used by this module.

A separate local preview was tested at http://localhost:5251 using the `RROMS_Sprint2_Preview` LocalDB database. Its records are sample reservations, separate from your configured application database.

## Group integration

Sprint 1 login should use the application cookie scheme and issue role `Customer` or `Restaurant Staff` (`Staff` also supported). Customer ownership uses the numeric `CustomerID` claim, falling back to `ClaimTypes.NameIdentifier`. A missing/invalid identifier never grants another customer's access. If your group uses ASP.NET Identity, retain its authentication registration and connect these policies and claims to that scheme instead of registering a second login system.

Sprint 3 must authorize ownership and check the persisted reservation status is `Seated` on every order submission. `Reservation.OrderingEnabled` exposes this condition for UI integration; Sprint 2 does not implement an order endpoint.

Sprint 4 should consume the booking's stored `MinimumSpend` and paid `DepositAmount`. Never apply forfeited deposits to new reservations. No final billing calculations or payment recording are implemented here.

Sprint 1 can provide dining areas/tables through the existing entities and operating hours through `RestaurantSetting.OpeningTime` and `ClosingTime`. Current defaults are 12:00–22:00 every day, in Malaysia time. Per-day closures and split lunch/dinner sessions are not yet represented by the shared settings model. These are integration points for the restaurant configuration module, not Firewood's copied hours.

## Booking rules

| Guests | Allowed dining area | Dining duration | Minimum spend |
|---|---|---|---|
| 1–3 | Indoor, Outdoor | 2 hours | RM50 per guest |
| 4–8 | Indoor, Outdoor | 2 hours | RM50 per guest |
| 4–8 | VIP Room | Until closing | RM2,400 per booking |
| 9–12 | VIP Room only | Until closing | RM3,600 per booking |

Reservations outside 1–12 guests are rejected. Customers choose the dining area, never the specific table. Allocation chooses the smallest available table/room of sufficient capacity, with table ID as a deterministic tie-breaker. Availability considers the complete requested interval, not just the physical table's current status. Adjacent bookings are allowed; overlapping bookings are blocked. Serializable SQL Server transactions protect allocation and subsequent changes from concurrent requests. A SQL deadlock is presented as a retry message for booking actions.

Only future half-hour slots inside opening hours are offered. Indoor and Outdoor need two full hours before closing. VIP bookings reserve the room from start until closing. Paid deposit and minimum spend are separate amounts. RM50 per guest is the agreed fixed reservation deposit, and minimum spend is captured at booking time.

Creating a booking starts a Pending payment hold for up to 10 minutes, ending earlier if the reservation starts before that. A recorded dummy payment reference and success/failure selector simulate the payment without processing real funds or collecting card details. Failed attempts are recorded; successful payment changes Pending to Confirmed. Repeated successful payment submissions are rejected. An unpaid expired hold becomes Expired and releases allocation. This is distinct from Cancelled and No-show.

One reschedule is allowed at least 168 hours before the original start. Only date/time change; party size, selected area, deposit and minimum spend are retained. Original start is preserved, and old/new date/time are recorded in ReservationHistory. Availability is checked again and a suitable table is automatically reassigned. Staff follow the same restrictions.

Cancellation of Pending or Confirmed bookings releases allocation. Any paid deposit is marked Forfeited; no refund function is present.

Staff check-in is available from the scheduled start up to the arrival deadline. It marks the booking Seated and activates the ordering integration flag. Before the 10-minute normal deadline, customers may notify late arrival, extending the deadline to 30 minutes after the scheduled start. This does not extend the dining end time. Expiry processing runs every 30 seconds while the application is running, and also on reservation page requests. An unchecked-in confirmed booking becomes No-show at its applicable deadline, its paid deposit is forfeited, and its table allocation is released. Seated bookings are not automatically marked No-show. Staff can complete a seated session and cannot mark a booking No-show before its deadline.

Staff table reassignment retains the selected dining area, verifies capacity and full interval availability, and is allowed before check-in. Staff can view contact details, reservation requests and the customer's booking history. Customers can view only their own records.

Confirmation, policy details, simulated deposit records and updates are available on the booking detail page, with a print action. Notifications are stored in-app. Customer confirmation and arrival emails are supported through the configurable SMTP sender described below. Reservation lists and details refresh after 30 seconds when idle. Editing a form pauses its automatic refresh.

## Architecture and database

Razor Pages provide the presentation layer. ReservationService, ReservationPolicy and TableAllocationService provide the business layer. ApplicationDbContext/EF Core and SQL Server provide persistence.

The additional Sprint2ReservationWorkflow migration adds original start, creation/check-in/cancellation timestamps, late-arrival flag, payment hold expiry, minimum spend and row-version concurrency control. It adds unique booking references and an allocation search index. Existing reservation dates and earliest recorded rescheduling history populate original start. Existing duration settings become 120 minutes; opening/closing defaults are 12:00/22:00. Existing bookings' scheduled end times are preserved during upgrade. The Sprint2DepositReceipt migration records simulation method and payment reference; older payment methods are labelled Not recorded rather than guessed.

The seed inventory is sample data. Actual area availability also depends on its configured table capacities, so an eligible dining area can still have no slot for a particular party.

## Verification

Build: `dotnet build RROMS/RROMS.csproj`

Policy checks: `dotnet run --project tests/RROMS.Sprint2.Tests`

SQL Server integration checks: `dotnet run --project tests/RROMS.Sprint2.Tests -- --integration`

The integration runner creates a uniquely named `RROMS_Sprint2_Test_...` LocalDB database and deletes only that owned test database afterward. It never uses the application connection string. Tests cover party-size boundaries, VIP minimums/durations, overlapping/adjacent reservations, migration of existing settings, ownership restrictions, simulated payment failure/retry/duplicate protection, modification restrictions for staff and customers, cancellation, expired unpaid holds, staff check-in/completion, both no-show deadlines and simultaneous attempts to book a single room.

Reference inspiration: https://firegroup.my/branch/firewood-penang-2/ . The design is original; restaurant policies come from the group's agreed requirements and subsequent VIP clarification.


## Customer confirmation and arrival emails

Successful deposit payment queues a confirmation email in the same database transaction as the booking confirmation. Failed payments and repeat payment requests do not queue confirmations. Valid rescheduling queues an updated confirmation. The message includes booking reference, date, Malaysia arrival/end time, party size, dining area, paid deposit, minimum spend, check-in instructions and cancellation/late-arrival policy.

The default arrival reminder is 24 hours before the reservation. Configure `Email:ReminderHours` as `[2]`, `[24]` or `[24,2]`. Positive intervals up to 168 hours are supported. Reminders whose due time was already before booking creation are omitted for last-minute bookings. After rescheduling, the previous booking time's queued mail is skipped and reminders are scheduled for the new time. Cancelled, expired, no-show, completed and seated reservations are not sent arrival reminders.

The background worker checks every 30 seconds while the app runs. SQL Server stores the mail queue, so queued messages survive restarts. Failed delivery is retried with a delay up to five attempts. Unique event keys prevent normal duplicate scheduling; send leases prevent two workers processing the same mail together. SMTP cannot guarantee exactly-once delivery if the process crashes after the provider accepts a message but before the queue records success. A stable Message-ID is retained on retry.

### Configure the sender

The `Email` section in `RROMS/appsettings.json` contains the Gmail sender configuration. Store `Email:Password` and local `Email:Enabled` in Visual Studio Manage User Secrets; never put passwords in the project settings file. Matching environment variables can also supply these settings. Set `Email:Host`, `Email:Port`, `Email:SenderAddress` and `Email:SenderName` for your provider. Use port 587 with required STARTTLS, or your provider's implicit TLS port with `SslOnConnect: true`. Certificate validation remains enabled.

Set authentication through local environment variables `Email__Username` and `Email__Password` (use the provider's SMTP/app credential). Do not commit credentials or send passwords in chat. Enable sending with `Email__Enabled=true` only after configuring the sender and using actual intended recipient accounts. SMTP is disabled by default; no external email was sent during verification. The tests replace the sender with an in-memory fake.

The development seed customer email is sample data, not a verified recipient address. Existing confirmed reservations are not retroactively sent confirmation emails. New successful deposits and valid rescheduling generate confirmations. Upcoming confirmed bookings can receive arrival reminders when email sending is enabled.

API reference: https://mimekit.net/docs/html/T_MailKit_Net_Smtp_SmtpClient.htm


## Céleste presentation

The Sprint 2 interface follows the supplied Céleste references: warm ivory (#F7F4EE), soft beige (#EDE7DC), charcoal (#2D2A26), muted olive (#7B8060), and champagne gold (#B59A68). Buttons use a deeper olive for readable white text. Cormorant Garamond, Playfair Display, and Inter are requested from Google Fonts, with serif/sans-serif fallbacks when unavailable.

The supplied indoor, outdoor, and private-room photographs are stored locally under wwwroot/images. The reservation page changes its photograph with the selected area and stacks the form below the photograph on smaller screens. The landing page introduces dining spaces and links into reservations. Menu ordering and account administration remain with their assigned sprints; no mock contact details or inactive menu links are presented.

Visual verification: desktop landing, booking and details pages; 390px booking layout without horizontal overflow; nine guests filter to VIP Room with RM450 deposit and RM3,600 minimum spend. Build succeeds with zero warnings and errors.

## Sprint 2 Pages reference update (14 October 2026)

Implemented the supplied desktop/mobile PNG references as functional Razor pages:
- Search uses photo area cards with immediate guest-count filtering and no automatic refresh.
- Availability is a separate step with five date choices and live available time slots.
- Guest Details displays the signed-in account's contact information and collects dietary preferences, allergies and special requests. These are combined into the existing reservation request field; account editing remains with Sprint 1.
- Continuing to deposit creates the pending reservation and its existing payment hold. Payment stays simulated, with successful/failed outcomes and dummy references. Success opens the confirmation view.
- Customer reservations have Upcoming/All tabs, image rows and links to separate reschedule and late-arrival views.
- Staff have a sidebar, daily counts, reference/name search, existing seating/reassignment controls and a live seating board by area/date/time.
- Existing service rules, ownership checks, automatic no-shows, email outbox and notification records remain in use. Tables are still assigned automatically when the pending payment hold is created; mockup sample seating, contact details and claims of real encrypted checkout are not used.

Verification: build with no warnings/errors; all 54 SQL Server workflow checks passed; browser booking from 9-guest VIP search on 28 October through successful simulated payment, preserving RM450 deposit and RM3,600 minimum spend. Desktop staff schedule reads that same booking. Mobile search and management layouts checked at 390px without horizontal overflow.


## Database cleanup

Removed eight unused columns through RemoveUnusedSprint2Attributes: Customers.Password; DiningAreas.AreaName and Description; RestaurantSettings.DepositPerPerson, ReservationDurationMinutes, MaxModificationCount, GracePeriod and LateArrivalPeriod. Their model properties, seed values and unused precision configuration were removed too. All nine application tables remain in use. RestaurantSettings retains SettingID, OpeningTime and ClosingTime; the agreed deposit, duration, modification and grace-period rules continue to be enforced in ReservationPolicy/ReservationService. The Gmail app password remains in User Secrets and is unrelated to the removed preview customer-password column.

Historical migrations remain necessary for existing databases. The new migration upgrades them without deleting customer, table, reservation, payment or notification rows. Integration checks verify the eight columns are absent after upgrading the original schema.

## File guide for group members

Each handwritten Sprint 2 source/view file now starts with a purpose comment. Important policy and transaction decisions have nearby comments too.

- Pages/Reservations: customer booking screens and their request handlers. Create handles search/time/guest steps; Details handles payment, confirmation and management; Index lists owned reservations.
- Pages/Staff: daily schedule, filters and the seating board. Staff actions are executed by the shared reservation service.
- Pages/Shared: common site/staff layout, booking progress and reservation summary.
- Models: database records. Navigation properties connect records; they are not separate database columns.
- Data: table mapping, migrations startup and sample seed data.
- Services/ReservationPolicy: fixed agreed rules and Malaysia time.
- Services/ReservationService: transaction-protected booking changes and expiry.
- Services/TableAllocationService: area/capacity/overlap checks.
- Services/ReservationEmailService: scheduled email queue and delivery retries; EmailService performs SMTP transport; EmailOptions reads configuration.
- Program.cs: service registration, authentication policies and request pipeline.
- wwwroot/css/reservations.css: shared desktop, mobile and print appearance.
- Migrations: ordered schema upgrades. Handwritten migration files explain their purpose; Designer files and the model snapshot are generated by Entity Framework.
- tests/RROMS.Sprint2.Tests: policy and isolated SQL workflow checks; no real emails are sent by these tests.

These comments explain the current implementation; they do not change booking rules or introduce additional functionality.
Formatting: the remaining C# sources, Razor pages and shared stylesheet use consistent indentation and readable blocks. The root .editorconfig records four-space indentation and C# block formatting for group members. Formatting changes do not alter reservation rules; both the website and verification project build successfully.
