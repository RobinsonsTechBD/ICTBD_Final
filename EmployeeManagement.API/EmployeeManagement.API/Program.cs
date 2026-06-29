using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Application.Services;
using EmployeeManagement.Domain.Entities;
using EmployeeManagement.Domain.Identity;
using EmployeeManagement.Infrastructure.AI;
using EmployeeManagement.Infrastructure.Persistence;
using EmployeeManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ---- MVC / Swagger (with JWT "Authorize" button) ----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EmployeeManagement API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT token only (no 'Bearer ' prefix needed here)."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// ---- Database ----
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---- Identity ----
builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ---- JWT Authentication ----
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key missing in appsettings.json");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization();

// ---- Named HttpClients for the two providers (add near other builder.Services calls) ----
builder.Services.AddHttpClient("OllamaLocal");
builder.Services.AddHttpClient("OllamaCloud");
// ---- Register both providers (the fallback engine picks the right one per model) ----
builder.Services.AddScoped<IChatProvider, OllamaLocalProvider>();
builder.Services.AddScoped<IChatProvider, OllamaCloudProvider>();

// ---- Register the fallback engine and the admin config service ----
builder.Services.AddScoped<IModelFallbackChatService, ModelFallbackChatService>();
builder.Services.AddScoped<IAIModelConfigService, AIModelConfigService>();

// ---- Register RAG services (add near your other AddScoped<> calls) ----
builder.Services.AddScoped<IEmbeddingService, OllamaEmbeddingService>();
builder.Services.AddScoped<ITextChunker, SimpleTextChunker>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IRagQueryService, RagQueryService>();


// ---- Application services ----
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<ILeaveService, LeaveService>();
builder.Services.AddScoped<IHolidayService, HolidayService>();
builder.Services.AddScoped<IShiftService, ShiftService>();
builder.Services.AddScoped<IAttendanceDeviceSimulatorService, AttendanceDeviceSimulatorService>();

// ---- Register Agent services (add near your other AddScoped<> calls) ----
builder.Services.AddScoped<IIntentClassifierService, IntentClassifierService>();
builder.Services.AddScoped<IAgentOrchestratorService, AgentOrchestratorService>();
builder.Services.AddScoped<IChatSessionService, ChatSessionService>();

// ---- Register Vision services (add near your other AddScoped<> calls) ----
builder.Services.AddScoped<IVisionAnalysisService, VisionAnalysisService>();
builder.Services.AddScoped<IVisionIndexService, VisionIndexService>();

// ---- Register AI SQL services ----
builder.Services.AddScoped<ISchemaContextService, SchemaContextService>();
builder.Services.AddScoped<ISqlSafetyGuard, SqlSafetyGuard>();
builder.Services.AddScoped<ISqlGenerationService, SqlGenerationService>();
builder.Services.AddScoped<ILiveQueryExecutor, LiveQueryExecutor>();
builder.Services.AddScoped<INlQueryService, NlQueryService>();

// ---- Register Employee + Department services ----
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();


builder.Services.AddCors(opt => opt.AddPolicy("AllowAngular", p =>
    p.WithOrigins(
        "http://localhost:4200",
        "https://localhost:4200"
     )
     .AllowAnyHeader()
     .AllowAnyMethod()));


var app = builder.Build();

// ---- Migrate + seed ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    string[] roles = { "Admin", "Manager", "Marketing", "Sales", "Purchase", "Delivery" };
    foreach (var role in roles)
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole<int>(role));

    if (!db.WorkShifts.Any())
    {
        db.WorkShifts.Add(new WorkShift
        {
            Name = "General Shift",
            StartTime = new TimeSpan(10, 0, 0),
            EndTime = new TimeSpan(19, 0, 0),
            GraceMinutes = 15,
            HalfDayThresholdMinutes = 240,
            IsActive = true
        });
        db.SaveChanges();
    }

    // Seed a default Admin login so you have something to log in with immediately.
    // CHANGE THIS PASSWORD before any real deployment.
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    const string adminEmail = "admin@modhumoti.com";
    if (await userManager.FindByEmailAsync(adminEmail) is null)
    {
        var adminEmployee = new Employee
        {
            EmployeeCode = "EMP-ADMIN",
            FullName = "System Administrator",
            Email = adminEmail,
            Role = "Admin",
            IsActive = true
        };
        db.Employees.Add(adminEmployee);
        db.SaveChanges();

        var adminUser = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmployeeId = adminEmployee.Id, IsActive = true };
        var result = await userManager.CreateAsync(adminUser, "Admin@123");
        if (result.Succeeded)
            await userManager.AddToRoleAsync(adminUser, "Admin");
    }

    // ============================================================================
    // SEED DATA — add inside your existing "using (var scope = app.Services...)"
    // seeding block, after the WorkShift seed. This is the actual fallback chain
    // from your spec: Qwen3.5 -> Llama3.1 -> Qwen3.5VL -> Gemma4.
    //
    // IMPORTANT: adjust ModelName to the EXACT tag you've pulled with `ollama pull`.
    // Run `ollama list` to see your actual installed tags — Ollama's public model
    // names don't always match the marketing names 1:1 (e.g. it's commonly
    // "qwen2.5:7b" rather than "qwen3.5"). Update the strings below accordingly.
    // ============================================================================

    if (!db.AIModelConfigs.Any())
    {
        db.AIModelConfigs.AddRange(
            new AIModelConfig { ModelName = "llama3.1:latest", Provider = ModelProviderType.Local, Priority = 1, IsEnabled = true, TimeoutSeconds = 30, SupportsVision = false },
            new AIModelConfig { ModelName = "llama3.2:latest", Provider = ModelProviderType.Local, Priority = 2, IsEnabled = true, TimeoutSeconds = 30, SupportsVision = false },
            new AIModelConfig { ModelName = "qwen3.5:0.8b", Provider = ModelProviderType.Local, Priority = 3, IsEnabled = true, TimeoutSeconds = 45, SupportsVision = true },
            new AIModelConfig { ModelName = "llava:latest", Provider = ModelProviderType.Cloud, Priority = 4, IsEnabled = false, TimeoutSeconds = 30, SupportsVision = false } // disabled until Ollama:CloudBaseUrl/CloudApiKey are set
        );
        db.SaveChanges();
    }

}



// ---- HTTP pipeline ----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAngular");
app.UseAuthentication();   // must come before UseAuthorization
app.UseAuthorization();

app.MapControllers();

app.Run();
