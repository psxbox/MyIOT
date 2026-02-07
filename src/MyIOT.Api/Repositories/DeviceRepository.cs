using Microsoft.EntityFrameworkCore;
using MyIOT.Api.Data;
using MyIOT.Api.Models;

namespace MyIOT.Api.Repositories;

public class DeviceRepository : IDeviceRepository
{
    private readonly AppDbContext _db;

    public DeviceRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Device?> GetByIdAsync(Guid id)
    {
        return await _db.Devices.FindAsync(id);
    }

    public async Task<Device?> GetByAccessTokenAsync(string accessToken)
    {
        return await _db.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.AccessToken == accessToken);
    }

    public async Task<List<Device>> GetAllAsync()
    {
        return await _db.Devices
            .AsNoTracking()
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<Device> CreateAsync(Device device)
    {
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();
        return device;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _db.Devices.AnyAsync(d => d.Id == id);
    }
}
