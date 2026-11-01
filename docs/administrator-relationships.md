# Administrator relationships and table usage

Administrators, StaffAccounts and Customers store separate account credentials. Administrators contains AdministratorID, Name, Email, NormalizedEmail and PasswordHash, plus active/demo flags, lockout, reset-token and session-version fields. Customers is constrained to the Customer role. The shared login page authenticates each account against its own table and issues distinct administrator/staff ID claims; only customer sessions receive CustomerID. Administrator permissions still include staff functions.

The account-separation migration copies legacy salted password hashes, preserves administrator IDs and audit links, moves legacy staff logins into StaffAccounts and removes their old customer account records. Existing internal sessions and reset links require renewal. It stops if an internal account has customer activity or a conflicting staff email, so no history or credentials are silently discarded. Back up the database before upgrade; rollback requires restoring that backup.

Administrator profile editing and password recovery now use Administrators. Customer registration rejects emails already assigned to an internal account. Passwords are never stored as plaintext.

Announcements, DiningAreas, RestaurantSettings, StaffAccounts, MenuCategories and MenuItems each reference Administrators through nullable CreatedByAdministratorID and UpdatedByAdministratorID foreign keys. Existing and seed data retain null attribution when no authenticated administrator performed the operation. Dashboard queries do not assign ownership of customer reservations, payments or analytics to administrators.

MenuItems now has a required MenuCategoryID foreign key. Category text remains a compatibility/display value for existing filters and historical integrations. The tracked save path resolves this text to a category; category rename and deletion use the foreign key. Existing catalogue names are backfilled before constraints are enabled.

All original 18 tables are used, so none were deleted. Reviews stores customer feedback on completed orders. Notifications stores booking updates and audit events; CustomerNotifications stores the customer inbox. ReservationEmails is the email outbox. RestaurantSettings controls hours. MenuCategories supports menu administration. Account, seating, booking, order and billing entities are used by their respective pages and services. Administrators brings the model total to 19 tables.

Deletion of referenced administrators, categories and accounts is restricted to preserve related records. Administrator attribution covers tracked SaveChanges operations; raw SQL and ExecuteUpdate operations bypass this hook and must handle attribution explicitly if introduced for managed records.

Diagrams: [ERD](diagrams/erd.md), [class diagram](diagrams/class-diagram.md). Mermaid source files are included for editing. The class diagram uses actual public properties; password/token properties are omitted for readability. Service signatures are abbreviated. Administrator and StaffAccount are independent account classes.

Validation: the SQL Server regression runner passes 204 checks, including legacy credential migration, administrator and staff shared login, role access, administrator profile editing, lockout, password recovery, cookie revocation, audit attribution, and existing reservation/payment workflows.
