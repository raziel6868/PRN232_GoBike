using BusinessObjects.Entities;
using BusinessObjects.Enums;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;

namespace Repositories;

public class MotorcycleRepository : Repository<Motorcycle>, IMotorcycleRepository
{
    public MotorcycleRepository(AppDbContext context) : base(context)
    {
    }

    public Task<List<Motorcycle>> GetAvailableAsync()
        => dbSet.Include(m => m.VehicleType)
                .Where(x => x.IsActive
                            && x.VehicleType != null
                            && x.VehicleType.IsActive
                            && x.Status == MotorcycleStatus.Available)
                .ToListAsync();

    public Task<bool> ExistsByLicensePlateAsync(string licensePlate, int? excludeId = null)
        => dbSet.AnyAsync(x => x.LicensePlate == licensePlate
                            && (!excludeId.HasValue || x.Id != excludeId.Value));

    public Task<bool> ExistsByRegistrationNoAsync(string registrationNo, int? excludeId = null)
        => dbSet.AnyAsync(x => x.RegistrationNo == registrationNo
                            && (!excludeId.HasValue || x.Id != excludeId.Value));

    public async Task<(List<Motorcycle> Items, int TotalCount)> SearchAsync(
        string? searchTerm,
        MotorcycleStatus? status,
        decimal? minPrice,
        decimal? maxPrice,
        int page,
        int pageSize)
    {
        var query = dbSet
            .Include(m => m.VehicleType)
            .Where(m => m.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(m =>
                m.LicensePlate.ToLower().Contains(term) ||
                m.Brand.ToLower().Contains(term) ||
                m.Model.ToLower().Contains(term));
        }

        if (status.HasValue)
            query = query.Where(m => m.Status == status.Value);

        if (minPrice.HasValue)
            query = query.Where(m => m.VehicleType != null && m.VehicleType.DefaultDailyRate >= minPrice.Value);

        if (maxPrice.HasValue)
            query = query.Where(m => m.VehicleType != null && m.VehicleType.DefaultDailyRate <= maxPrice.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<Motorcycle?> GetByIdWithDetailsAsync(int id)
        => dbSet.Include(m => m.VehicleType)
                .Include(m => m.RentalContracts.OrderByDescending(r => r.CreatedAt).Take(5))
                    .ThenInclude(r => r.Customer)
                .FirstOrDefaultAsync(m => m.Id == id);

    public Task<bool> HasActiveRentalsAsync(int motorcycleId)
        => context.RentalContracts.AnyAsync(r =>
            r.MotorcycleId == motorcycleId &&
            (r.Status == RentalStatus.Active || r.Status == RentalStatus.Reserved));
}
