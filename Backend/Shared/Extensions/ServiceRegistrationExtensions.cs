using ActoEngine.WebApi.Features.Auth;
using ActoEngine.WebApi.Features.Clients;
using ActoEngine.WebApi.Features.Context;
using ActoEngine.WebApi.Features.ErDiagram;
using ActoEngine.WebApi.Features.FormBuilder;
using ActoEngine.WebApi.Features.ImpactAnalysis;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Aggregation;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Approval;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Contracts;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Graph;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Pathing;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.Scoring;
using ActoEngine.WebApi.Features.ImpactAnalysis.Engine.VerdictBuilder;
using ActoEngine.WebApi.Features.LogicalFk;
using ActoEngine.WebApi.Features.Permissions;
using ActoEngine.WebApi.Features.ProjectClients;
using ActoEngine.WebApi.Features.Projects;
using ActoEngine.WebApi.Features.Roles;
using ActoEngine.WebApi.Features.Schema;
using ActoEngine.WebApi.Features.SpBuilder;
using ActoEngine.WebApi.Features.Users;
using ActoEngine.WebApi.Infrastructure.Database;
using ActoEngine.WebApi.Services.ValidationService;
using ActoEngine.WebApi.Shared.Validation;

namespace ActoEngine.WebApi.Shared.Extensions
{
    public static class ServiceRegistrationExtensions
    {
        public static void AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddLogging(logging => logging.AddConsole());
            services.AddApiServices(configuration);

            // Configure batch settings from appsettings.json
            services.Configure<BatchSettings>(configuration.GetSection("BatchSettings"));
            services.Configure<DatabaseSeedingOptions>(configuration.GetSection("DatabaseSeeding"));


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

            // Logical FK Services
            services.AddScoped<ILogicalFkRepository, LogicalFkRepository>();
            services.AddScoped<ILogicalFkService, LogicalFkService>();
            services.AddSingleton(configuration.GetSection("DetectionConfig").Get<DetectionConfig>() ?? new DetectionConfig());
            services.AddScoped<ConfidenceCalculator>();

            // ER Diagram Services
            services.AddScoped<IErDiagramRepository, ErDiagramRepository>();
            services.AddScoped<IErDiagramService, ErDiagramService>();

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

            services.AddSingleton<IPasswordHasher, PasswordHasher>();
            services.AddSingleton<ITokenHasher, TokenHasher>();

            // Validation Services
            services.AddSingleton<IPasswordValidator, PasswordValidator>();
            services.AddSingleton<IDatabaseIdentifierValidator, DatabaseIdentifierValidator>();

            // SSE Connection Management
            services.AddSingleton<SseConnectionManager>();
            services.AddScoped<ISseTicketService, SseTicketService>();

            services.AddTransient<DatabaseMigrator>();
        }
    }
}
