using OctopusPLM.Core.Entities;

namespace OctopusPLM.Core.Interfaces;

public interface IProductRepository
{
    Task<List<Product>> GetListAsync(int page, int size, long? categoryId, string? keyword);
    Task<Product?> GetByIdAsync(long id);
    Task<Product> AddAsync(Product product);
    Task UpdateAsync(Product product);
    Task DeleteAsync(long id);
}
