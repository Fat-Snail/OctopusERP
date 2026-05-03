using OctopusPLM.Core.Entities;

namespace OctopusPLM.Core.Interfaces;

public interface ICategoryRepository
{
    Task<List<Category>> GetAllAsync();
    Task<List<Category>> GetChildrenAsync(long parentId);
    Task<Category?> GetByIdAsync(long id);
    Task<Category> AddAsync(Category category);
    Task UpdateAsync(Category category);
    Task DeleteAsync(long id);
}
