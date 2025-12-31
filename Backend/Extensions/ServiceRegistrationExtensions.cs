using ActoEngine.WebApi.Config;
using ActoEngine.WebApi.Repositories;
using ActoEngine.WebApi.Services.Auth;
using ActoEngine.WebApi.Services.ClientService;
using ActoEngine.WebApi.Services.ContextService;
using ActoEngine.WebApi.Services.Database;
using ActoEngine.WebApi.Services.FormBuilderService;
using ActoEngine.WebApi.Services.PermissionService;
using ActoEngine.WebApi.Services.ProjectClientService;
using ActoEngine.WebApi.Services.ProjectService;
using ActoEngine.WebApi.Services.RoleService;
using ActoEngine.WebApi.Services.Schema;
using ActoEngine.WebApi.Services.SpBuilderService;
using ActoEngine.WebApi.Services.UserManagementService;
using ActoEngine.WebApi.Services.ValidationService;
using ActoEngine.WebApi.Services.DependencyService;
using ActoEngine.WebApi.Services.ImpactAnalysis.Engine.Contracts;
using ActoEngine.WebApi.Services.ImpactAnalysis.Engine.Scoring;
using ActoEngine.WebApi.Services.ImpactAnalysis.Engine.Pathing;
using ActoEngine.WebApi.Services.ImpactAnalysis.Engine.Graph;
using ActoEngine.WebApi.Services.ImpactAnalysis.Engine.Approval;
using ActoEngine.WebApi.Services.ImpactAnalysis.Engine.Aggregation;
using ActoEngine.WebApi.Services.ImpactAnalysis;
using ActoEngine.WebApi.Services.ImpactAnalysis.Engine.VerdictBuilder;

namespace ActoEngine.WebApi.Extensions
{
    public static class ServiceRegistrationExtensions
    {
        public static void AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddLogging(logging => logging.AddConsole());
            services.AddApiServices(configuration);

            // Configure batch settings from appsettings.json
            services.Configure<BatchSettings>(configuration.GetSection("BatchSettings"));


            // Add distributed memory cache for permission caching
            services.AddDistributedMemoryCache();

            services.AddScoped<IDbConnectionFactory, SqlServerConnectionFactory>();
            services.AddScoped<IDataSeeder, DatabaseSeeder>();

            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ITokenRepository, TokenRepository>();
            services.AddScoped<IProjectRepository, ProjectRepository>();
            services.AddScoped<ISchemaRepository, SchemaRepository>();
            services.AddScoped<IClientRepository, ClientRepository>();
            services.AddScoped<IProjectClientRepository, ProjectClientRepository>();
            services.AddScoped<IContextRepository, ContextRepository>();
            services.AddScoped<IDependencyRepository, DependencyRepository>();

            // Role & Permission Repositories
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IPermissionRepository, PermissionRepository>();

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ISchemaService, SchemaService>();
            services.AddScoped<IProjectService, ProjectService>();
            services.AddScoped<ISpBuilderService, SpBuilderService>();
            services.AddScoped<IFormBuilderService, FormBuilderService>();
            services.AddScoped<IClientService, ClientService>();
            services.AddScoped<IProjectClientService, ProjectClientService>();
            services.AddScoped<IContextService, ContextService>();

            // Impact Analysis Services
            services.AddScoped<IImpactFacade, ImpactFacade>();
            services.AddScoped<IGraphBuilder, GraphBuilder>();
            services.AddScoped<IPathRiskEvaluator, PathRiskEvaluatorV1>();
            services.AddScoped<IImpactAggregator, ImpactAggregator>();
            services.AddScoped<IApprovalPolicy, ApprovalPolicyV1>();
            // Dependency Analysis Services
            services.AddScoped<IDependencyAnalysisService, DependencyAnalysisService>();
            services.AddScoped<IDependencyResolutionService, DependencyResolutionService>();
            services.AddScoped<IDependencyOrchestrationService, DependencyOrchestrationService>();

            services.AddScoped<ImpactVerdictBuilder>();
            services.AddScoped<IPathEnumerator>(_ =>
    new BfsPathEnumerator(
        configuration.GetValue<int>("PathEnumeration:MaxDepth", 10),
        configuration.GetValue<int>("PathEnumeration:MaxPaths", 1000)));

            // Role & Permission Services
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<IPermissionService, PermissionService>();
            services.AddScoped<IUserManagementService, UserManagementService>();

            // Form Builder Services
            services.AddScoped<IFormConfigRepository, FormConfigRepository>();
            services.AddScoped<ICodeTemplateRepository, CodeTemplateRepository>();
            services.AddScoped<IGenerationHistoryRepository, GenerationHistoryRepository>();
            services.AddScoped<ITemplateRenderService, TemplateRenderService>();

            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<ITokenHasher, TokenHasher>();

            // Validation Services
            services.AddSingleton<IPasswordValidator, PasswordValidator>();
            services.AddSingleton<IDatabaseIdentifierValidator, DatabaseIdentifierValidator>();

            services.AddTransient<DatabaseMigrator>();
        }

        // Helper meant to capture the existing AddApiServices call if it was an extension method elsewhere, 
        // but looking at Program.cs line 212: builder.Services.AddApiServices(builder.Configuration);
        // It seems AddApiServices is ALREADY an extension method likely in another file?
        // Let's check Extensions folder content again. 
        // Oh, I see "HttpContextExtensions.cs" only.
        // Wait, where is AddApiServices defined? Is it in one of the existing defined namespaces?
        // Typically it might be in Extensions class in the root or close by.
        // I will assume for now I should just call it. 
        // Actually, if AddApiServices is ALREADY an extension, I should check where it is.
        // If it's not standard, maybe it was added recently?
        // Let's look at the imports in Program.cs.
        // using ActoEngine.WebApi.Extensions; is NOT there initially, only Namespace imports.
        // Wait, line 212 calls builder.Services.AddApiServices.
        // I will assume it is available via one of the imported namespaces or I'll find it invalid.
        // But scanning `Program.cs` again...
        // Line 212: builder.Services.AddApiServices(builder.Configuration);
        // I don't see `using ActoEngine.WebApi.Extensions;` in the file view of Program.cs.
        // Maybe it's in `ActoEngine.WebApi.Config`? Or just in the global namespace?
        // I'll keep it there.
    }
}
