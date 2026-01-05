using Domain.Entities;

namespace Infrastructure.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<Category>> GetUserCategoriesAsync(long userId, CancellationToken cancellationToken = default);
    Task<Category?> GetCategoryByIdAsync(long userId, int categoryId, CancellationToken cancellationToken = default);
    Task<Category> CreateAsync(long userId, string name, Domain.Enums.TransactionType type, string? icon = null, CancellationToken cancellationToken = default);
    Task<Category?> RenameAsync(long userId, int categoryId, string newName, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long userId, int categoryId, CancellationToken cancellationToken = default);
    Task<Category?> GetByNameAsync(long userId, string name, CancellationToken cancellationToken = default);
    Task InitializeDefaultCategoriesAsync(long userId, CancellationToken cancellationToken = default);
}
