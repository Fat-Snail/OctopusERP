using OctopusPLM.Core.Entities;

namespace OctopusPLM.Core.Interfaces;

public interface IAttributeRepository
{
    Task<List<PlmAttribute>> GetAllAsync();
    Task<PlmAttribute?> GetByIdAsync(long id);
    Task<List<PlmAttribute>> GetByCategoryAsync(long categoryId);
    Task<PlmAttribute> AddAsync(PlmAttribute attribute);
    Task UpdateAsync(PlmAttribute attribute);
    Task DeleteAsync(long id);
}
