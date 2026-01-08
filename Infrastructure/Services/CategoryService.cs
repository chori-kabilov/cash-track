using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class CategoryService(DataContext context) : ICategoryService
{
    public async Task<IReadOnlyList<Category>> GetUserCategoriesAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await context.Categories
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.IsActive)
            .OrderBy(c => c.Priority)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Category?> GetCategoryByIdAsync(long userId, int categoryId, CancellationToken cancellationToken = default)
    {
        return await context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);
    }

    public async Task<Category?> GetByNameAsync(long userId, string name, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim();
        return await context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Name.ToLower() == normalized.ToLower(), cancellationToken);
    }

    public async Task<Category> CreateAsync(long userId, string name, TransactionType type, string? icon = null, CancellationToken cancellationToken = default)
    {
        var category = new Category
        {
            UserId = userId,
            Name = name.Trim(),
            Icon = icon,
            Type = type,
            Priority = Priority.Optional,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Categories.Add(category);
        await context.SaveChangesAsync(cancellationToken);
        return category;
    }

    public async Task<Category?> RenameAsync(long userId, int categoryId, string newName, CancellationToken cancellationToken = default)
    {
        var category = await context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);
        if (category == null)
            return null;

        category.Name = newName.Trim();
        await context.SaveChangesAsync(cancellationToken);
        return category;
    }

    public async Task<bool> DeleteAsync(long userId, int categoryId, CancellationToken cancellationToken = default)
    {
        var category = await context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken);
        if (category == null)
            return false;

        // Soft delete
        category.IsActive = false;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task InitializeDefaultCategoriesAsync(long userId, CancellationToken cancellationToken = default)
    {
        if (await context.Categories.AnyAsync(c => c.UserId == userId, cancellationToken))
            return;

        var now = DateTimeOffset.UtcNow;
        var defaultCategories = new[]
        {
            // Expenses
            new Category { UserId = userId, Name = "Еда", Icon = "🍕", Type = TransactionType.Expense, Priority = Priority.Required, CreatedAt = now },
            new Category { UserId = userId, Name = "Маршрут", Icon = "🚌", Type = TransactionType.Expense, Priority = Priority.Required, CreatedAt = now },
            new Category { UserId = userId, Name = "Развлечение", Icon = "🎮", Type = TransactionType.Expense, Priority = Priority.Preferred, CreatedAt = now },
            new Category { UserId = userId, Name = "Учёба", Icon = "📚", Type = TransactionType.Expense, Priority = Priority.Preferred, CreatedAt = now },
            new Category { UserId = userId, Name = "Карьера", Icon = "💼", Type = TransactionType.Expense, Priority = Priority.Preferred, CreatedAt = now },
            new Category { UserId = userId, Name = "Интернет", Icon = "📱", Type = TransactionType.Expense, Priority = Priority.Required, CreatedAt = now },
            new Category { UserId = userId, Name = "Накопления", Icon = "🎯", Type = TransactionType.Expense, Priority = Priority.Required, CreatedAt = now },
            new Category { UserId = userId, Name = "Проект X", Icon = "🥷", Type = TransactionType.Expense, Priority = Priority.Preferred, CreatedAt = now },
            new Category { UserId = userId, Name = "Одежда", Icon = "👕", Type = TransactionType.Expense, Priority = Priority.Preferred, CreatedAt = now },
            new Category { UserId = userId, Name = "Дом", Icon = "🏠", Type = TransactionType.Expense, Priority = Priority.Required, CreatedAt = now },
            new Category { UserId = userId, Name = "Здоровье", Icon = "💊", Type = TransactionType.Expense, Priority = Priority.Required, CreatedAt = now },
            new Category { UserId = userId, Name = "Кафе", Icon = "☕", Type = TransactionType.Expense, Priority = Priority.Optional, CreatedAt = now },
            
            // Income
            new Category { UserId = userId, Name = "Зарплата", Icon = "💰", Type = TransactionType.Income, Priority = Priority.Required, CreatedAt = now },
            new Category { UserId = userId, Name = "Фриланс", Icon = "💻", Type = TransactionType.Income, Priority = Priority.Preferred, CreatedAt = now },
            new Category { UserId = userId, Name = "Подарок", Icon = "🎁", Type = TransactionType.Income, Priority = Priority.Optional, CreatedAt = now },
            new Category { UserId = userId, Name = "Возврат долга", Icon = "🤝", Type = TransactionType.Income, Priority = Priority.Optional, CreatedAt = now },
            new Category { UserId = userId, Name = "Бизнес", Icon = "🏢", Type = TransactionType.Income, Priority = Priority.Preferred, CreatedAt = now },
            new Category { UserId = userId, Name = "Инвестиции", Icon = "📈", Type = TransactionType.Income, Priority = Priority.Optional, CreatedAt = now },
            new Category { UserId = userId, Name = "Прочее", Icon = "📝", Type = null, Priority = Priority.Optional, CreatedAt = now }
        };

        context.Categories.AddRange(defaultCategories);
        await context.SaveChangesAsync(cancellationToken);
    }

    // Получить по типу
    public async Task<IReadOnlyList<Category>> GetByTypeAsync(long userId, TransactionType type, CancellationToken ct = default)
    {
        return await context.Categories.AsNoTracking()
            .Where(c => c.UserId == userId && c.Type == type && c.IsActive)
            .OrderBy(c => c.Priority)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);
    }

    // Обновить
    public async Task<Category?> UpdateAsync(long userId, int categoryId, string name, string? icon, Priority priority, CancellationToken ct = default)
    {
        var category = await context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, ct);
        if (category == null) return null;

        category.Name = name.Trim();
        category.Icon = icon;
        category.Priority = priority;
        await context.SaveChangesAsync(ct);
        return category;
    }

    // Архивировать
    public async Task<Category?> ArchiveAsync(long userId, int categoryId, CancellationToken ct = default)
    {
        var category = await context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId && c.IsActive, ct);
        if (category == null) return null;

        category.IsActive = false;
        await context.SaveChangesAsync(ct);
        return category;
    }

    // Восстановить
    public async Task<Category?> RestoreAsync(long userId, int categoryId, CancellationToken ct = default)
    {
        var category = await context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId && !c.IsActive, ct);
        if (category == null) return null;

        category.IsActive = true;
        await context.SaveChangesAsync(ct);
        return category;
    }
}

