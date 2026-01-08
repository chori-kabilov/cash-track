using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Services;

public interface ICategoryService
{
    // READ
    Task<IReadOnlyList<Category>> GetUserCategoriesAsync(long userId, CancellationToken ct = default);
    Task<IReadOnlyList<Category>> GetByTypeAsync(long userId, TransactionType type, CancellationToken ct = default);
    Task<Category?> GetCategoryByIdAsync(long userId, int categoryId, CancellationToken ct = default);
    Task<Category?> GetByNameAsync(long userId, string name, CancellationToken ct = default);
    
    // CREATE
    Task<Category> CreateAsync(long userId, string name, TransactionType type, string? icon = null, CancellationToken ct = default);
    Task InitializeDefaultCategoriesAsync(long userId, CancellationToken ct = default);
    
    // UPDATE
    Task<Category?> RenameAsync(long userId, int categoryId, string newName, CancellationToken ct = default);
    Task<Category?> UpdateAsync(long userId, int categoryId, string name, string? icon, Priority priority, CancellationToken ct = default);
    
    // ARCHIVE
    Task<Category?> ArchiveAsync(long userId, int categoryId, CancellationToken ct = default);
    Task<Category?> RestoreAsync(long userId, int categoryId, CancellationToken ct = default);
    Task<bool> DeleteAsync(long userId, int categoryId, CancellationToken ct = default);
}
