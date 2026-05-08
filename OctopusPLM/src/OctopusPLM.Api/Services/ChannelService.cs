using Microsoft.EntityFrameworkCore;
using OctopusPLM.Api.DTOs;
using OctopusPLM.Infrastructure.Persistence;

namespace OctopusPLM.Api.Services;

public class ChannelService
{
    private readonly PlmDbContext _db;

    public ChannelService(PlmDbContext db) => _db = db;

    // ── Channel CRUD ──────────────────────────────────────────────────────────

    public async Task<List<ChannelDefResponse>> GetAllChannelsAsync()
    {
        return await _db.ChannelDefs
            .OrderBy(c => c.ChannelCode)
            .Select(c => new ChannelDefResponse
            {
                ChannelId = c.ChannelId,
                ChannelCode = c.ChannelCode,
                ChannelName = c.ChannelName,
                Status = c.Status,
                CreatedAt = c.CreatedAt,
            })
            .ToListAsync();
    }

    public async Task<(Core.Entities.ChannelDef? Channel, string? Error)> CreateChannelAsync(string channelCode, string channelName)
    {
        if (string.IsNullOrWhiteSpace(channelCode)) return (null, "渠道编码不能为空");
        if (string.IsNullOrWhiteSpace(channelName)) return (null, "渠道名称不能为空");
        if (await _db.ChannelDefs.AnyAsync(c => c.ChannelCode == channelCode))
            return (null, "渠道编码已存在");

        var channel = new Core.Entities.ChannelDef
        {
            ChannelCode = channelCode,
            ChannelName = channelName,
            Status = 1,
            CreatedAt = DateTime.UtcNow,
        };
        _db.ChannelDefs.Add(channel);
        await _db.SaveChangesAsync();
        return (channel, null);
    }

    public async Task<string?> UpdateChannelAsync(long id, string channelName, int status)
    {
        var channel = await _db.ChannelDefs.FindAsync(id);
        if (channel == null) return "渠道不存在";
        channel.ChannelName = channelName;
        channel.Status = status;
        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> DeleteChannelAsync(long id)
    {
        if (await _db.ChannelCategoryMappings.AnyAsync(m => m.ChannelId == id))
            return "该渠道下有类目映射，请先删除后再删渠道";
        var channel = await _db.ChannelDefs.FindAsync(id);
        if (channel == null) return "渠道不存在";
        _db.ChannelDefs.Remove(channel);
        await _db.SaveChangesAsync();
        return null;
    }

    // ── Category Mapping CRUD ─────────────────────────────────────────────────

    public async Task<List<ChannelCategoryMappingItem>> GetCategoryMappingsAsync(long channelId)
    {
        var mappings = await _db.ChannelCategoryMappings
            .Where(m => m.ChannelId == channelId)
            .Include(m => m.Category)
            .Include(m => m.AttributeMappings)
            .ThenInclude(am => am.Attribute)
            .OrderBy(m => m.MappingId)
            .ToListAsync();

        return mappings.Select(m => new ChannelCategoryMappingItem
        {
            MappingId = m.MappingId,
            ChannelId = m.ChannelId,
            CategoryId = m.CategoryId,
            CategoryName = m.Category?.Name ?? "",
            ExternalCategoryId = m.ExternalCategoryId,
            ExternalCategoryName = m.ExternalCategoryName,
            MappingVersion = m.MappingVersion,
            Status = m.Status,
            CreatedAt = m.CreatedAt,
            AttributeMappings = m.AttributeMappings
                .OrderBy(am => am.MappingId)
                .Select(am => new ChannelAttrMappingItem
                {
                    MappingId = am.MappingId,
                    AttributeId = am.AttributeId,
                    AttributeName = am.Attribute?.Name ?? "",
                    AttributeCode = am.Attribute?.Code,
                    ExternalAttributeId = am.ExternalAttributeId,
                    ExternalAttributeName = am.ExternalAttributeName,
                    IsRequiredOutbound = am.IsRequiredOutbound,
                    TransformRuleJson = am.TransformRuleJson,
                    Status = am.Status,
                })
                .ToList(),
        }).ToList();
    }

    public async Task<(Core.Entities.ChannelCategoryMapping? Mapping, string? Error)> CreateCategoryMappingAsync(
        long channelId, long categoryId, string externalCategoryId, string externalCategoryName, string mappingVersion)
    {
        if (await _db.ChannelDefs.FindAsync(channelId) == null) return (null, "渠道不存在");
        if (await _db.Categories.FindAsync(categoryId) == null) return (null, "类目不存在");
        if (await _db.ChannelCategoryMappings.AnyAsync(m =>
            m.ChannelId == channelId && m.CategoryId == categoryId && m.MappingVersion == mappingVersion))
            return (null, "该渠道下已有此类目和版本的映射");

        var mapping = new Core.Entities.ChannelCategoryMapping
        {
            ChannelId = channelId,
            CategoryId = categoryId,
            ExternalCategoryId = externalCategoryId,
            ExternalCategoryName = externalCategoryName,
            MappingVersion = string.IsNullOrWhiteSpace(mappingVersion) ? "v1" : mappingVersion,
            Status = 1,
            CreatedAt = DateTime.UtcNow,
        };
        _db.ChannelCategoryMappings.Add(mapping);
        await _db.SaveChangesAsync();
        return (mapping, null);
    }

    public async Task<string?> UpdateCategoryMappingAsync(long channelId, long mappingId, string externalCategoryId, string externalCategoryName, int status)
    {
        var mapping = await _db.ChannelCategoryMappings.FindAsync(mappingId);
        if (mapping == null || mapping.ChannelId != channelId) return "映射不存在";
        mapping.ExternalCategoryId = externalCategoryId;
        mapping.ExternalCategoryName = externalCategoryName;
        mapping.Status = status;
        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> DeleteCategoryMappingAsync(long channelId, long mappingId)
    {
        var mapping = await _db.ChannelCategoryMappings
            .Include(m => m.AttributeMappings)
            .FirstOrDefaultAsync(m => m.MappingId == mappingId && m.ChannelId == channelId);
        if (mapping == null) return "映射不存在";
        _db.ChannelAttributeMappings.RemoveRange(mapping.AttributeMappings);
        _db.ChannelCategoryMappings.Remove(mapping);
        await _db.SaveChangesAsync();
        return null;
    }

    // ── Attribute Mapping CRUD ────────────────────────────────────────────────

    public async Task<(Core.Entities.ChannelAttributeMapping? Mapping, string? Error)> CreateAttributeMappingAsync(
        long channelId, long categoryMappingId, long attributeId,
        string externalAttributeId, string externalAttributeName,
        bool isRequiredOutbound, string? transformRuleJson)
    {
        var catMapping = await _db.ChannelCategoryMappings.FindAsync(categoryMappingId);
        if (catMapping == null || catMapping.ChannelId != channelId) return (null, "类目映射不存在");
        if (await _db.Attributes.FindAsync(attributeId) == null) return (null, "属性不存在");
        if (await _db.ChannelAttributeMappings.AnyAsync(m =>
            m.ChannelCategoryMappingId == categoryMappingId && m.AttributeId == attributeId))
            return (null, "该属性在此类目映射中已存在");

        var mapping = new Core.Entities.ChannelAttributeMapping
        {
            ChannelCategoryMappingId = categoryMappingId,
            AttributeId = attributeId,
            ExternalAttributeId = externalAttributeId,
            ExternalAttributeName = externalAttributeName,
            IsRequiredOutbound = isRequiredOutbound,
            TransformRuleJson = transformRuleJson,
            Status = 1,
            CreatedAt = DateTime.UtcNow,
        };
        _db.ChannelAttributeMappings.Add(mapping);
        await _db.SaveChangesAsync();
        return (mapping, null);
    }

    public async Task<string?> UpdateAttributeMappingAsync(
        long channelId, long categoryMappingId, long attrMappingId,
        string externalAttributeId, string externalAttributeName,
        bool isRequiredOutbound, string? transformRuleJson)
    {
        var catMapping = await _db.ChannelCategoryMappings.FindAsync(categoryMappingId);
        if (catMapping == null || catMapping.ChannelId != channelId) return "类目映射不存在";

        var attrMapping = await _db.ChannelAttributeMappings.FindAsync(attrMappingId);
        if (attrMapping == null || attrMapping.ChannelCategoryMappingId != categoryMappingId) return "属性映射不存在";

        attrMapping.ExternalAttributeId = externalAttributeId;
        attrMapping.ExternalAttributeName = externalAttributeName;
        attrMapping.IsRequiredOutbound = isRequiredOutbound;
        attrMapping.TransformRuleJson = transformRuleJson;
        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> DeleteAttributeMappingAsync(long channelId, long categoryMappingId, long attrMappingId)
    {
        var catMapping = await _db.ChannelCategoryMappings.FindAsync(categoryMappingId);
        if (catMapping == null || catMapping.ChannelId != channelId) return "类目映射不存在";

        var attrMapping = await _db.ChannelAttributeMappings.FindAsync(attrMappingId);
        if (attrMapping == null || attrMapping.ChannelCategoryMappingId != categoryMappingId) return "属性映射不存在";

        _db.ChannelAttributeMappings.Remove(attrMapping);
        await _db.SaveChangesAsync();
        return null;
    }
}
