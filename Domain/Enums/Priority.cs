namespace Domain.Enums;

/// <summary>
/// Priority level for categories and other entities.
/// </summary>
public enum Priority
{
    /// <summary>
    /// 🔴 Обязательно — critical expenses that must be paid.
    /// </summary>
    Required = 1,

    /// <summary>
    /// 🟡 Желательно — preferred but not critical.
    /// </summary>
    Preferred = 2,

    /// <summary>
    /// 🟢 Можно и без этого — optional, can skip.
    /// </summary>
    Optional = 3
}
