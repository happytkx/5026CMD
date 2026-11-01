# Administrator relationships and table usage

The Administrators table is a one-to-zero-or-one profile of Customers. Existing administrator login credentials, password recovery and cookie validation stay in the account record. Creating a profile does not grant a role; access remains controlled by the account role and server authorisation policies.

Announcements, DiningAreas, RestaurantSettings, StaffAccounts, MenuCategories and MenuItems each reference Administrators through nullable CreatedByAdministratorID and UpdatedByAdministratorID foreign keys. Existing and seed data retain null attribution when no authenticated administrator performed the operation. Dashboard queries do not assign ownership of customer reservations, payments or analytics to administrators.

MenuItems now has a required MenuCategoryID foreign key. Category text remains a compatibility/display value for existing filters and historical integrations. The tracked save path resolves this text to a category; category rename and deletion use the foreign key. Existing catalogue names are backfilled before constraints are enabled.

All original 18 tables are used, so none were deleted. Reviews stores customer feedback on completed orders. Notifications stores booking updates and audit events; CustomerNotifications stores the customer inbox. ReservationEmails is the email outbox. RestaurantSettings controls hours. MenuCategories supports menu administration. Account, seating, booking, order and billing entities are used by their respective pages and services. Administrators brings the model total to 19 tables.

Deletion of referenced administrators, categories and accounts is restricted to preserve related records. Administrator attribution covers tracked SaveChanges operations; raw SQL and ExecuteUpdate operations bypass this hook and must handle attribution explicitly if introduced for managed records.

Diagrams: [ERD](diagrams/erd.md), [class diagram](diagrams/class-diagram.md). Mermaid source files are included for editing. The class diagram uses actual public properties; password/token properties are omitted for readability. Service signatures are abbreviated. Administrator is a profile, not a subclass of StaffAccount.
