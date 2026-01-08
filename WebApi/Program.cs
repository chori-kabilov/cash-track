using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Infrastructure.Data;
using Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// === База данных ===
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<DataContext>(opt => opt.UseNpgsql(connectionString));

// === Сервисы ===
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IGoalService, GoalService>();
builder.Services.AddScoped<IDebtService, DebtService>();
builder.Services.AddScoped<IRegularPaymentService, RegularPaymentService>();
builder.Services.AddScoped<ILimitService, LimitService>();

// === Контроллеры и Swagger ===
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CashTrack API",
        Version = "v1.0",
        Description = "API для управления CashTrack"
    });
    c.EnableAnnotations();
});

var app = builder.Build();

// Swagger всегда включён
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CashTrack API v1");
    c.RoutePrefix = string.Empty;
});

app.MapControllers();
app.Run("http://[::]:5000");
