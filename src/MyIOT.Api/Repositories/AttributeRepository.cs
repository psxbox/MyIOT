using Microsoft.EntityFrameworkCore;
using MyIOT.Api.Data;
using MyIOT.Api.Models;
using MyIOT.Shared.Models;

namespace MyIOT.Api.Repositories;

public class AttributeRepository : IAttributeRepository
{
    private readonly AppDbContext _db;

    public AttributeRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Insert or update an attribute. Uses the unique index (device_id, key, scope).
    /// </summary>
    public async Task UpsertAsync(DeviceAttribute attribute)
    {
        var existing = await _db.DeviceAttributes
            .FirstOrDefaultAsync(a => a.DeviceId == attribute.DeviceId
                                   && a.Key == attribute.Key
                                   && a.Scope == attribute.Scope);

        if (existing is not null)
        {
            existing.Value = attribute.Value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            attribute.Id = Guid.NewGuid();
            attribute.UpdatedAt = DateTime.UtcNow;
            _db.DeviceAttributes.Add(attribute);
        }

        await _db.SaveChangesAsync();
    }

    public async Task UpsertBatchAsync(IEnumerable<DeviceAttribute> attributes)
    {
        foreach (var attr in attributes)
        {
            var existing = await _db.DeviceAttributes
                .FirstOrDefaultAsync(a => a.DeviceId == attr.DeviceId
                                       && a.Key == attr.Key
                                       && a.Scope == attr.Scope);

            if (existing is not null)
            {
                existing.Value = attr.Value;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                attr.Id = Guid.NewGuid();
                attr.UpdatedAt = DateTime.UtcNow;
                _db.DeviceAttributes.Add(attr);
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<List<DeviceAttribute>> GetByDeviceAsync(Guid deviceId, AttributeScope? scope = null)
    {
        var query = _db.DeviceAttributes
            .AsNoTracking()
            .Where(a => a.DeviceId == deviceId);

        if (scope.HasValue)
            query = query.Where(a => a.Scope == scope.Value);

        return await query
            .OrderBy(a => a.Key)
            .ToListAsync();
    }
}
