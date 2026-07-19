using BusinessObjects.Entities;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using BusinessObjects.Enums;
using System.Linq;

namespace Repositories;

public class RentalContractRepository : Repository<RentalContract>, IRentalContractRepository
{
    public RentalContractRepository(AppDbContext context) : base(context)
    {
    }

    public override Task<List<RentalContract>> GetAllAsync()
        => dbSet
            .Include(x => x.Customer)
            .Include(x => x.Motorcycle)
                .ThenInclude(m => m!.VehicleType)
            .Include(x => x.Inspections)
            .Include(x => x.Payments)
            .Include(x => x.CreatedByUser)
            .ToListAsync();

    public override Task<RentalContract?> GetByIdAsync(int id)
        => dbSet
            .Include(x => x.Customer)
            .Include(x => x.Motorcycle)
                .ThenInclude(m => m!.VehicleType)
            .Include(x => x.Inspections)
            .Include(x => x.Payments)
            .Include(x => x.CreatedByUser)
            .FirstOrDefaultAsync(x => x.Id == id);

    public Task<List<RentalContract>> GetByCustomerIdAsync(int customerId)
        => dbSet.Where(x => x.CustomerId == customerId).ToListAsync();

    public Task<List<RentalContract>> GetByMotorcycleIdAsync(int motorcycleId)
        => dbSet.Where(x => x.MotorcycleId == motorcycleId).ToListAsync();

    public Task<List<RentalContract>> SearchAsync(int? customerId, int? motorcycleId, RentalStatus? status, DateTime? fromDate, DateTime? toDate, int page, int pageSize)
    {
        var query = dbSet
            .Include(x => x.Customer)
            .Include(x => x.Motorcycle)
                .ThenInclude(m => m!.VehicleType)
            .Include(x => x.CreatedByUser)
            .AsQueryable();

        if (customerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        if (motorcycleId.HasValue)
        {
            query = query.Where(x => x.MotorcycleId == motorcycleId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.RentalDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.RentalDate <= toDate.Value);
        }

        return query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
}
