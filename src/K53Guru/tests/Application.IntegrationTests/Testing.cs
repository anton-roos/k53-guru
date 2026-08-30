using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using K53Guru.Application.Common.Interfaces;
using K53Guru.Application.Common.Interfaces.Identity;
using K53Guru.Domain.Identity;
using K53Guru.Infrastructure;
using K53Guru.Application.Common.Extensions;
using K53Guru.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NUnit.Framework;
using Respawn;
using Respawn.Graph;
using K53Guru.Application.Features.PicklistSets.DTOs;
using K53Guru.Infrastructure.Services;
using K53Guru.Application.Features.Tenants.DTOs;
using K53Guru.Infrastructure.Services.MultiTenant;

namespace K53Guru.Application.IntegrationTests;

[SetUpFixture]
public class Testing
{
    private static IConfigurationRoot _configuration;
    private static IServiceScopeFactory _scopeFactory;
    private static Respawner _checkpoint;
    private static string _currentUserId;
    private static string[] _currentUserRoles = Array.Empty<string>();

    [OneTimeSetUp]
    public async Task RunBeforeAnyTests()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true)
            .AddEnvironmentVariables();

        _configuration = builder.Build();

        //var startup = new Startup(_configuration);

        var services = new ServiceCollection();

        services.AddSingleton(Mock.Of<IWebHostEnvironment>(w =>
            w.EnvironmentName == "Development" &&
            w.ApplicationName == "Server.UI"));

        services.AddInfrastructure(_configuration)
            .AddApplication();

        //services.AddLogging();

        //startup.ConfigureServices(services);

        // 替换 IUserContextAccessor 的注册
        var userContextServiceDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IUserContextAccessor));
        if (userContextServiceDescriptor != null)
        {
            services.Remove(userContextServiceDescriptor);
        }

        // 使用 Moq 创建 Mock 对象并配置 Current 属性
        services.AddSingleton<IUserContextAccessor>(provider =>
        {
            var mockUserContextAccessor = new Mock<IUserContextAccessor>();
            // This accessor is registered as a singleton (matching the real UserContextAccessor's
            // lifetime), so this factory only runs once for the whole test run. Current must
            // therefore be evaluated lazily (Returns(Func<>)) rather than baked in as a fixed
            // value here - otherwise it would freeze at whichever user/roles were current the
            // first time any test resolved this service, ignoring every later
            // RunAsAdministratorAsync()/RunAsDefaultUserAsync() call.
            mockUserContextAccessor.Setup(x => x.Current).Returns(() =>
                string.IsNullOrEmpty(_currentUserId)
                    ? null
                    : new UserContext(_currentUserId, "admin", null, "admin@example.com",
                        Roles: _currentUserRoles));
            return mockUserContextAccessor.Object;
        });

        _scopeFactory = services.BuildServiceProvider().GetService<IServiceScopeFactory>();
        EnsureDatabase();
        _checkpoint = await Respawner.CreateAsync(
            _configuration.GetValue<string>("DatabaseSettings:ConnectionString"),
            new RespawnerOptions
            {
                TablesToIgnore = new Table[] { "__EFMigrationsHistory" }
            });

        
    }

    private static void EnsureDatabase()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetService<ApplicationDbContext>();
        context.Database.Migrate();
    }

    public static async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetService<IMediator>();
        return await mediator.Send(request);
    }

    public static async Task<string> RunAsDefaultUserAsync()
    {
        return await RunAsUserAsync("Demo", "Password123!", new string[] { });
    }

    public static async Task<string> RunAsAdministratorAsync()
    {
        return await RunAsUserAsync("administrator", "Password123!", new[] { "Admin" });
    }

    public static async Task<string> RunAsUserAsync(string userName, string password, string[] roles)
    {
        using var scope = _scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = userName, Email = userName };
        var result = await userManager.CreateAsync(user, password);

        if (roles.Any())
        {
            var roleManager = scope.ServiceProvider.GetService<RoleManager<IdentityRole>>();
            foreach (var role in roles)
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
            await userManager.AddToRolesAsync(user, roles);
        }

        if (result.Succeeded)
        {
            _currentUserId = user.Id;
            _currentUserRoles = roles;
            return _currentUserId;
        }

        var errors = string.Join(Environment.NewLine, result.ToApplicationResult().Errors);
        throw new Exception($"Unable to create {userName}.{Environment.NewLine}{errors}");
    }

    public static async Task ResetState()
    {
        await _checkpoint.ResetAsync(_configuration.GetValue<string>("DatabaseSettings:ConnectionString"));
        _currentUserId = null;
        _currentUserRoles = Array.Empty<string>();
    }

    public static async Task<TEntity> FindAsync<TEntity>(params object[] keyValues)
        where TEntity : class
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetService<ApplicationDbContext>();
        return await context.FindAsync<TEntity>(keyValues);
    }
    public static IApplicationDbContext CreateDbContext()
    {
        var scope = _scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    public static async Task AddAsync<TEntity>(TEntity entity)
        where TEntity : class
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetService<ApplicationDbContext>();
        context.Add(entity);
        await context.SaveChangesAsync();
    }

    public static async Task<int> CountAsync<TEntity>() where TEntity : class
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetService<ApplicationDbContext>();
        return await context.Set<TEntity>().CountAsync();
    }

    public static IDataSourceService<PicklistSetDto> CreatePicklistService()
    {
        var scope = _scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<PicklistDataSourceService>();
    }

    public static IDataSourceService<TenantDto> CreateTenantsService()
    {
        var scope = _scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TenantDataSourceService>();
    }

    [OneTimeTearDown]
    public void RunAfterAnyTests()
    {
    }
}
