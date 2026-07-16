using BusinessObjects.Entities;
using BusinessObjects;
using BusinessObjects.Enums;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Repositories;
using Services.DTOs;
using Services.Interfaces;

namespace Services;

public class RentalContractService : IRentalContractService
{
    private const int MaxAdvanceBookingDays = 2;
    private readonly AppDbContext context;
    private readonly IRentalContractRepository rentalContractRepository;

    public RentalContractService(AppDbContext context, IRentalContractRepository rentalContractRepository)
    {
        this.context = context;
        this.rentalContractRepository = rentalContractRepository;
    }

    public Task<List<RentalContract>> GetAllAsync()
        => rentalContractRepository.GetAllAsync();

    public Task<RentalContract?> GetByIdAsync(int id)
        => rentalContractRepository.GetByIdAsync(id);

    public async Task<RentalContract> ReserveAsync(ReserveRentalRequestDto request, int? userId)
    {
        ValidateDepositConfirmation(request.DepositConfirmed);
        ValidateDepositProofImage(request.DepositProofImageUrl);
        ValidatePaymentMethod(request.DepositPaymentMethod, nameof(request.DepositPaymentMethod));
        ValidateDateRange(request.StartDate, request.EndDate);
        ValidateReservationStartDate(request.StartDate);
        ValidateAdvanceBookingLimit(request.StartDate);

        await using var transaction = await context.Database.BeginTransactionAsync();
        var (customer, motorcycle) = await LoadAndValidateContractPartiesAsync(request.CustomerId, request.MotorcycleId);
        ValidateMotorcycleAvailable(motorcycle);

        var contract = CreateBaseContract(request.CustomerId, request.MotorcycleId, request.StartDate, request.EndDate, motorcycle, userId);
        contract.Status = RentalStatus.Reserved;
        contract.Notes = request.Notes;

        motorcycle.Status = MotorcycleStatus.Reserved;
        motorcycle.UpdatedAt = SystemClock.Now;

        context.RentalContracts.Add(contract);
        await context.SaveChangesAsync();

        context.RentalPayments.Add(CreatePayment(
            contract.Id,
            PaymentType.Deposit,
            contract.DepositAmount,
            request.DepositPaymentMethod,
            request.DepositPaymentNote,
            userId,
            request.DepositProofImageUrl));
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await LoadContractAsync(contract.Id) ?? contract;
    }

    public async Task<RentalContract> RentNowAsync(RentNowRequestDto request, int? userId)
    {
        ValidateDepositConfirmation(request.DepositConfirmed);
        ValidateDepositProofImage(request.DepositProofImageUrl);
        ValidatePaymentMethod(request.DepositPaymentMethod, nameof(request.DepositPaymentMethod));
        ValidateDateRange(request.StartDate, request.EndDate);
        ValidateRentNowStartDate(request.StartDate);
        ValidateInspection(request.BeforeInspection);

        await using var transaction = await context.Database.BeginTransactionAsync();
        var (customer, motorcycle) = await LoadAndValidateContractPartiesAsync(request.CustomerId, request.MotorcycleId);
        ValidateMotorcycleAvailable(motorcycle);
        ValidateMileageNotDecreased(
            request.BeforeInspection.Mileage,
            motorcycle.Mileage,
            "Start mileage",
            "the motorcycle's current mileage");

        var contract = CreateBaseContract(request.CustomerId, request.MotorcycleId, request.StartDate, request.EndDate, motorcycle, userId);
        contract.Status = RentalStatus.Active;
        contract.Notes = request.Notes;

        motorcycle.Status = MotorcycleStatus.Rented;
        motorcycle.Mileage = request.BeforeInspection.Mileage;
        motorcycle.UpdatedAt = SystemClock.Now;

        context.RentalContracts.Add(contract);
        await context.SaveChangesAsync();

        context.RentalInspections.Add(CreateInspection(contract.Id, InspectionType.BeforeRental, request.BeforeInspection, userId));
        context.RentalPayments.Add(CreatePayment(
            contract.Id,
            PaymentType.Deposit,
            contract.DepositAmount,
            request.DepositPaymentMethod,
            request.DepositPaymentNote,
            userId,
            request.DepositProofImageUrl));
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await LoadContractAsync(contract.Id) ?? contract;
    }

    public async Task<RentalContract> HandoverAsync(int id, HandoverRentalRequestDto request, int? userId)
    {
        ValidateInspection(request.BeforeInspection);

        await using var transaction = await context.Database.BeginTransactionAsync();
        var contract = await LoadContractAsync(id)
            ?? throw new InvalidOperationException($"RentalContract with ID {id} not found");

        if (contract.Status != RentalStatus.Reserved)
            throw new InvalidOperationException("Only reserved contracts can be handed over");

        if (SystemClock.Today < contract.StartDate.Date)
            throw new InvalidOperationException("Cannot hand over motorcycle before StartDate");

        if (contract.Motorcycle is null || contract.Motorcycle.Status != MotorcycleStatus.Reserved)
            throw new InvalidOperationException("Motorcycle must be reserved before handover");

        if (contract.Inspections.Any(i => i.InspectionType == InspectionType.BeforeRental))
            throw new InvalidOperationException("BeforeRental inspection already exists");

        ValidateMileageNotDecreased(
            request.BeforeInspection.Mileage,
            contract.Motorcycle.Mileage,
            "Start mileage",
            "the motorcycle's current mileage");

        if (!contract.Payments.Any(p => p.PaymentType == PaymentType.Deposit))
            throw new InvalidOperationException("Deposit payment is required before handover");

        contract.Status = RentalStatus.Active;
        contract.UpdatedAt = SystemClock.Now;
        contract.UpdatedByUserId = userId;

        contract.Motorcycle.Status = MotorcycleStatus.Rented;
        contract.Motorcycle.Mileage = request.BeforeInspection.Mileage;
        contract.Motorcycle.UpdatedAt = SystemClock.Now;

        context.RentalInspections.Add(CreateInspection(contract.Id, InspectionType.BeforeRental, request.BeforeInspection, userId));
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await LoadContractAsync(contract.Id) ?? contract;
    }

    public async Task<RentalContract> CompleteAsync(int id, CompleteRentalRequestDto request, int? userId)
    {
        ValidateInspection(request.AfterInspection);
        ValidateCompleteRequest(request);

        await using var transaction = await context.Database.BeginTransactionAsync();
        var contract = await LoadContractAsync(id)
            ?? throw new InvalidOperationException($"RentalContract with ID {id} not found");

        if (contract.Status != RentalStatus.Active)
            throw new InvalidOperationException("Only active contracts can be completed");

        var beforeRentalInspection = contract.Inspections
            .FirstOrDefault(i => i.InspectionType == InspectionType.BeforeRental)
            ?? throw new InvalidOperationException("BeforeRental inspection is required before completing a contract");

        if (contract.Inspections.Any(i => i.InspectionType == InspectionType.AfterReturn))
            throw new InvalidOperationException("AfterReturn inspection already exists");

        ValidateMileageNotDecreased(
            request.AfterInspection.Mileage,
            beforeRentalInspection.Mileage,
            "Return mileage",
            "start mileage");

        if (request.ActualReturnDate.Date < contract.StartDate.Date)
            throw new InvalidOperationException("ActualReturnDate cannot be before StartDate");

        if (contract.Motorcycle is null)
            throw new InvalidOperationException("Motorcycle not found");

        var actualRentalDays = CalculateRentalDays(contract.StartDate, request.ActualReturnDate);
        var plannedRentalDays = CalculateRentalDays(contract.StartDate, contract.EndDate);
        var baseRentalDays = Math.Min(actualRentalDays, plannedRentalDays);

        contract.ActualReturnDate = request.ActualReturnDate.Date;
        contract.RentalDays = actualRentalDays;
        contract.TotalAmount = baseRentalDays * contract.DailyPrice;
        contract.LateDays = Math.Max(0, actualRentalDays - plannedRentalDays);
        contract.LateFee = contract.LateDays * contract.DailyPrice;
        contract.DamageFee = request.DamageFee;
        contract.DamageDescription = request.DamageDescription?.Trim();
        contract.OtherFee = request.OtherFee;
        contract.OtherFeeDescription = request.OtherFeeDescription?.Trim();
        contract.DiscountAmount = request.DiscountAmount;
        contract.DiscountReason = request.DiscountReason?.Trim();
        contract.FinalAmount = contract.TotalAmount + contract.LateFee + contract.DamageFee + contract.OtherFee - contract.DiscountAmount;

        if (contract.FinalAmount < 0)
            throw new InvalidOperationException("FinalAmount cannot be negative");

        contract.RemainingAmount = contract.FinalAmount - contract.DepositAmount;
        contract.AdditionalPaymentAmount = contract.RemainingAmount > 0 ? contract.RemainingAmount : 0;
        contract.RefundAmount = contract.RemainingAmount < 0 ? Math.Abs(contract.RemainingAmount) : 0;
        contract.Status = RentalStatus.Completed;
        contract.CompletedAt = SystemClock.Now;
        contract.CompletedByUserId = userId;
        contract.UpdatedAt = SystemClock.Now;
        contract.UpdatedByUserId = userId;

        var requiresMaintenance = request.MotorcycleStatusAfterReturn == MotorcycleStatus.Maintenance ||
                                  request.AfterInspection.HasDamage ||
                                  request.DamageFee > 0;

        contract.Motorcycle.Status = requiresMaintenance
            ? MotorcycleStatus.Maintenance
            : request.MotorcycleStatusAfterReturn;
        contract.Motorcycle.Mileage = request.AfterInspection.Mileage;
        contract.Motorcycle.UpdatedAt = SystemClock.Now;

        context.RentalInspections.Add(CreateInspection(contract.Id, InspectionType.AfterReturn, request.AfterInspection, userId));

        if (requiresMaintenance)
            context.MaintenanceRecords.Add(CreateMaintenanceRecord(contract, request, userId));

        if (contract.AdditionalPaymentAmount > 0)
        {
            var method = RequireSettlementPaymentMethod(request.SettlementPaymentMethod);
            context.RentalPayments.Add(CreatePayment(contract.Id, PaymentType.AdditionalPayment, contract.AdditionalPaymentAmount, method, request.SettlementPaymentNote, userId));
        }
        else if (contract.RefundAmount > 0)
        {
            var method = RequireSettlementPaymentMethod(request.SettlementPaymentMethod);
            context.RentalPayments.Add(CreatePayment(contract.Id, PaymentType.Refund, contract.RefundAmount, method, request.SettlementPaymentNote, userId));
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await LoadContractAsync(contract.Id) ?? contract;
    }

    public async Task<RentalContract> CancelAsync(int id, CancelRentalRequestDto request, int? userId)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("Cancellation reason is required");

        ValidatePaymentMethod(request.RefundPaymentMethod, nameof(request.RefundPaymentMethod));

        await using var transaction = await context.Database.BeginTransactionAsync();
        var contract = await LoadContractAsync(id)
            ?? throw new InvalidOperationException($"RentalContract with ID {id} not found");

        if (contract.Status != RentalStatus.Reserved)
            throw new InvalidOperationException("Only reserved contracts can be cancelled");

        var depositPaid = contract.Payments.Any(p => p.PaymentType == PaymentType.Deposit);
        contract.CancellationFee = depositPaid ? Math.Round(contract.DepositAmount * 0.5m, 2) : 0;
        contract.RefundAmount = depositPaid ? contract.DepositAmount - contract.CancellationFee : 0;
        contract.AdditionalPaymentAmount = 0;
        contract.FinalAmount = 0;
        contract.RemainingAmount = depositPaid ? contract.CancellationFee - contract.DepositAmount : 0;
        contract.Status = RentalStatus.Cancelled;
        contract.CancelledAt = SystemClock.Now;
        contract.CancelledByUserId = userId;
        contract.CancellationReason = request.Reason.Trim();
        contract.UpdatedAt = SystemClock.Now;
        contract.UpdatedByUserId = userId;

        if (contract.Motorcycle is not null)
        {
            contract.Motorcycle.Status = MotorcycleStatus.Available;
            contract.Motorcycle.UpdatedAt = SystemClock.Now;
        }

        if (contract.RefundAmount > 0)
            context.RentalPayments.Add(CreatePayment(contract.Id, PaymentType.Refund, contract.RefundAmount, request.RefundPaymentMethod, request.RefundPaymentNote, userId));

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await LoadContractAsync(contract.Id) ?? contract;
    }

    public async Task<RentalContract> MarkNoShowAsync(int id, NoShowRentalRequestDto request, int? userId)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("NoShow reason is required");

        await using var transaction = await context.Database.BeginTransactionAsync();
        var contract = await LoadContractAsync(id)
            ?? throw new InvalidOperationException($"RentalContract with ID {id} not found");

        if (contract.Status != RentalStatus.Reserved)
            throw new InvalidOperationException("Only reserved contracts can be marked as NoShow");

        if (SystemClock.Today < contract.StartDate.Date)
            throw new InvalidOperationException("Cannot mark NoShow before StartDate");

        var depositPaid = contract.Payments.Any(p => p.PaymentType == PaymentType.Deposit);
        contract.CancellationFee = depositPaid ? contract.DepositAmount : 0;
        contract.RefundAmount = 0;
        contract.AdditionalPaymentAmount = 0;
        contract.FinalAmount = 0;
        contract.RemainingAmount = 0;
        contract.Status = RentalStatus.NoShow;
        contract.NoShowAt = SystemClock.Now;
        contract.NoShowByUserId = userId;
        contract.NoShowReason = request.Reason.Trim();
        contract.UpdatedAt = SystemClock.Now;
        contract.UpdatedByUserId = userId;

        if (contract.Motorcycle is not null)
        {
            contract.Motorcycle.Status = MotorcycleStatus.Available;
            contract.Motorcycle.UpdatedAt = SystemClock.Now;
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return await LoadContractAsync(contract.Id) ?? contract;
    }

    private async Task<RentalContract?> LoadContractAsync(int id)
        => await context.RentalContracts
            .Include(c => c.Customer)
            .Include(c => c.Motorcycle)
                .ThenInclude(m => m!.VehicleType)
            .Include(c => c.Inspections)
            .Include(c => c.Payments)
            .FirstOrDefaultAsync(c => c.Id == id);

    private async Task<(Customer Customer, Motorcycle Motorcycle)> LoadAndValidateContractPartiesAsync(int customerId, int motorcycleId)
    {
        var customer = await context.Customers.FirstOrDefaultAsync(c => c.Id == customerId)
            ?? throw new InvalidOperationException($"Customer with ID {customerId} not found");

        if (!customer.IsActive)
            throw new InvalidOperationException("Customer is inactive");

        if (customer.Age < 18)
            throw new InvalidOperationException("Customer must be at least 18 years old");

        if (string.IsNullOrWhiteSpace(customer.DriverLicenseNo))
            throw new InvalidOperationException("Customer must have a driver license");

        var motorcycle = await context.Motorcycles
            .Include(m => m.VehicleType)
            .FirstOrDefaultAsync(m => m.Id == motorcycleId)
            ?? throw new InvalidOperationException($"Motorcycle with ID {motorcycleId} not found");

        if (!motorcycle.IsActive)
            throw new InvalidOperationException("Motorcycle is inactive");

        if (motorcycle.VehicleType is null)
            throw new InvalidOperationException("Motorcycle type not found");

        if (!motorcycle.VehicleType.IsActive)
            throw new InvalidOperationException("Motorcycle type is inactive");

        if (motorcycle.VehicleType.DefaultDailyRate <= 0)
            throw new InvalidOperationException("Motorcycle type daily rate must be greater than zero");

        if (motorcycle.VehicleType.DefaultDepositAmount <= 0)
            throw new InvalidOperationException("Motorcycle type deposit amount must be greater than zero");

        return (customer, motorcycle);
    }

    private static RentalContract CreateBaseContract(int customerId, int motorcycleId, DateTime startDate, DateTime endDate, Motorcycle motorcycle, int? userId)
    {
        var dailyPrice = motorcycle.VehicleType!.DefaultDailyRate;
        var depositAmount = motorcycle.VehicleType.DefaultDepositAmount;
        var rentalDays = CalculateRentalDays(startDate, endDate);

        return new RentalContract
        {
            CustomerId = customerId,
            MotorcycleId = motorcycleId,
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            DailyPrice = dailyPrice,
            DepositAmount = depositAmount,
            RentalDays = rentalDays,
            TotalAmount = rentalDays * dailyPrice,
            CreatedAt = SystemClock.Now,
            CreatedByUserId = userId,
            CreatedBy = userId?.ToString()
        };
    }

    private static RentalInspection CreateInspection(int contractId, InspectionType type, RentalInspectionRequestDto request, int? userId)
        => new()
        {
            RentalContractId = contractId,
            InspectionType = type,
            Mileage = request.Mileage,
            FuelLevel = request.FuelLevel.Trim(),
            VehicleCondition = request.VehicleCondition.Trim(),
            HasDamage = request.HasDamage,
            DamageDescription = request.DamageDescription?.Trim(),
            AccessoriesNote = request.AccessoriesNote?.Trim(),
            ImageUrl = request.ImageUrl?.Trim(),
            Note = request.Note?.Trim(),
            CreatedByUserId = userId,
            CreatedAt = SystemClock.Now
        };

    private static RentalPayment CreatePayment(
        int contractId,
        PaymentType type,
        decimal amount,
        PaymentMethod method,
        string? note,
        int? userId,
        string? proofImageUrl = null)
        => new()
        {
            RentalContractId = contractId,
            PaymentType = type,
            Amount = amount,
            PaymentMethod = method,
            Note = note?.Trim(),
            ProofImageUrl = proofImageUrl?.Trim(),
            CreatedByUserId = userId,
            CreatedAt = SystemClock.Now
        };

    private static MaintenanceRecord CreateMaintenanceRecord(RentalContract contract, CompleteRentalRequestDto request, int? userId)
    {
        var reason = request.AfterInspection.HasDamage || request.DamageFee > 0
            ? "Damage found after rental return"
            : "Motorcycle returned for maintenance";

        var descriptionParts = new List<string>
        {
            $"Return inspection condition: {request.AfterInspection.VehicleCondition.Trim()}"
        };

        if (!string.IsNullOrWhiteSpace(request.DamageDescription))
            descriptionParts.Add($"Damage fee note: {request.DamageDescription.Trim()}");

        if (!string.IsNullOrWhiteSpace(request.AfterInspection.DamageDescription))
            descriptionParts.Add($"Inspection damage: {request.AfterInspection.DamageDescription.Trim()}");

        if (!string.IsNullOrWhiteSpace(request.AfterInspection.AccessoriesNote))
            descriptionParts.Add($"Accessories: {request.AfterInspection.AccessoriesNote.Trim()}");

        if (!string.IsNullOrWhiteSpace(request.AfterInspection.Note))
            descriptionParts.Add($"Inspection note: {request.AfterInspection.Note.Trim()}");

        return new MaintenanceRecord
        {
            MotorcycleId = contract.MotorcycleId,
            RentalContractId = contract.Id,
            Reason = reason,
            Description = string.Join(Environment.NewLine, descriptionParts),
            RepairCost = 0,
            Status = MaintenanceStatus.Pending,
            StartDate = SystemClock.Today,
            CreatedByUserId = userId,
            CreatedAt = SystemClock.Now
        };
    }

    private static void ValidateDepositConfirmation(bool depositConfirmed)
    {
        if (!depositConfirmed)
            throw new InvalidOperationException("Deposit must be collected before creating or activating a contract");
    }

    private static void ValidateDepositProofImage(string? proofImageUrl)
    {
        if (string.IsNullOrWhiteSpace(proofImageUrl))
            throw new InvalidOperationException("Deposit proof image is required before creating a contract");

        if (!proofImageUrl.StartsWith("/uploads/rental-deposits/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Deposit proof image must be uploaded through the rental form");
    }

    private static void ValidateMotorcycleAvailable(Motorcycle motorcycle)
    {
        if (motorcycle.Status != MotorcycleStatus.Available)
            throw new InvalidOperationException("Only available motorcycles can be rented or reserved");
    }

    private static void ValidateDateRange(DateTime startDate, DateTime endDate)
    {
        if (endDate.Date < startDate.Date)
            throw new InvalidOperationException("EndDate must be greater than or equal to StartDate");
    }

    private static void ValidateAdvanceBookingLimit(DateTime startDate)
    {
        if (startDate.Date > SystemClock.Today.AddDays(MaxAdvanceBookingDays))
            throw new InvalidOperationException($"StartDate cannot be more than {MaxAdvanceBookingDays} days from today");
    }

    private static void ValidateReservationStartDate(DateTime startDate)
    {
        if (startDate.Date < SystemClock.Today)
            throw new InvalidOperationException("StartDate cannot be in the past");
    }

    private static void ValidateRentNowStartDate(DateTime startDate)
    {
        if (startDate.Date != SystemClock.Today)
            throw new InvalidOperationException("Rent-now contract must start today");
    }

    private static void ValidateInspection(RentalInspectionRequestDto request)
    {
        if (request.Mileage < 0)
            throw new InvalidOperationException("Inspection mileage cannot be negative");

        if (string.IsNullOrWhiteSpace(request.FuelLevel))
            throw new InvalidOperationException("FuelLevel is required");

        if (string.IsNullOrWhiteSpace(request.VehicleCondition))
            throw new InvalidOperationException("VehicleCondition is required");

        if (request.HasDamage && string.IsNullOrWhiteSpace(request.DamageDescription))
            throw new InvalidOperationException("DamageDescription is required when inspection has damage");
    }

    private static void ValidateMileageNotDecreased(int mileage, int minimumMileage, string fieldName, string referenceName)
    {
        if (mileage < minimumMileage)
            throw new InvalidOperationException($"{fieldName} cannot be lower than {referenceName} ({minimumMileage} km)");
    }

    private static void ValidateCompleteRequest(CompleteRentalRequestDto request)
    {
        if (request.DamageFee < 0 || request.OtherFee < 0 || request.DiscountAmount < 0)
            throw new InvalidOperationException("Fees and discount cannot be negative");

        if (request.DamageFee > 0 && string.IsNullOrWhiteSpace(request.DamageDescription))
            throw new InvalidOperationException("DamageDescription is required when DamageFee is greater than zero");

        if (request.OtherFee > 0 && string.IsNullOrWhiteSpace(request.OtherFeeDescription))
            throw new InvalidOperationException("OtherFeeDescription is required when OtherFee is greater than zero");

        if (request.DiscountAmount > 0 && string.IsNullOrWhiteSpace(request.DiscountReason))
            throw new InvalidOperationException("DiscountReason is required when DiscountAmount is greater than zero");

        if (request.MotorcycleStatusAfterReturn != MotorcycleStatus.Available &&
            request.MotorcycleStatusAfterReturn != MotorcycleStatus.Maintenance)
            throw new InvalidOperationException("Motorcycle status after return must be Available or Maintenance");
    }

    private static void ValidatePaymentMethod(PaymentMethod method, string fieldName)
    {
        if (!Enum.IsDefined(typeof(PaymentMethod), method))
            throw new InvalidOperationException($"{fieldName} is invalid");
    }

    private static PaymentMethod RequireSettlementPaymentMethod(PaymentMethod? method)
    {
        if (!method.HasValue || !Enum.IsDefined(typeof(PaymentMethod), method.Value))
            throw new InvalidOperationException("SettlementPaymentMethod is required when additional payment or refund is created");

        return method.Value;
    }

    private static int CalculateRentalDays(DateTime startDate, DateTime endDate)
        => Math.Max(1, (endDate.Date - startDate.Date).Days + 1);
}
