namespace BusinessObjects.Enums;

public enum UserRole
{
    Admin = 1,
    Staff = 2,
    Customer = 3
}

public enum MotorcycleStatus
{
    Available = 1,
    Reserved = 2,
    Rented = 3,
    Maintenance = 4
}

public enum RentalStatus
{
    Reserved = 1,
    Active = 2,
    Completed = 3,
    Cancelled = 4,
    NoShow = 5
}

public enum InspectionType
{
    BeforeRental = 1,
    AfterReturn = 2
}

public enum PaymentType
{
    Deposit = 1,
    AdditionalPayment = 2,
    Refund = 3
}

public enum PaymentMethod
{
    Cash = 1,
    BankTransfer = 2
}

public enum MaintenanceStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}
