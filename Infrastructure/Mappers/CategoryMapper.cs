using Domain.DTOs;
using Domain.Entities;

namespace Infrastructure.Mappers;

// Маппер для Category
public static class CategoryMapper
{
    public static CategoryDto ToDto(Category c)
    {
        return new CategoryDto(
            c.Id,
            c.Name,
            c.Icon,
            c.Type,
            c.Priority,
            c.IsActive
        );
    }
}
