using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using K53Guru.Application;
using K53Guru.Application.Common.Constants;
using K53Guru.Application.Common.Interfaces;
using K53Guru.Infrastructure.Services.Identity;
using K53Guru.Server.UI.Hubs;
using K53Guru.Server.UI.Middlewares;
using K53Guru.Server.UI.Services;
using K53Guru.Server.UI.Services.Identity;
using K53Guru.Server.UI.Services.JsInterop;
using K53Guru.Server.UI.Services.Layout;
using K53Guru.Server.UI.Services.Navigation;
using K53Guru.Server.UI.Services.Notifications;
using K53Guru.Server.UI.Services.UserPreferences;
using K53Guru.Server.UI; // For App component
using Hangfire;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using System.Threading.RateLimiting;
using Microsoft.Extensions.FileProviders;
using MudBlazor.Services;
using QuestPDF;
using QuestPDF.Infrastructure;



namespace K53Guru.Server.UI;

/// <summary>
/// Provides dependency injection configuration for the server UI.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds server UI services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">The configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddServerUI(this IServiceCollection services, IConfiguration config)
    {
        services.AddRazorComponents().AddInteractiveServerComponents().AddHubOptions(options=> options.MaximumReceiveMessageSize = 64 * 1024);
        services.AddCascadingAuthenticationState();
        services.AddMudServices(config =>
        {
      
            config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomCenter;
            config.SnackbarConfiguration.NewestOnTop = false;
            config.SnackbarConfiguration.ShowCloseIcon = true;
            config.SnackbarConfiguration.VisibleStateDuration = 3000;
            config.SnackbarConfiguration.HideTransitionDuration = 500;
            config.SnackbarConfiguration.ShowTransitionDuration = 500;
            config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
            config.SnackbarConfiguration.PreventDuplicates = false;

        });
        services.AddMudPopoverService();
        services.AddMudBlazorSnackbar();
        services.AddMudBlazorDialog();


        services.AddScoped<LocalizationCookiesMiddleware>()
            .Configure<RequestLocalizationOptions>(options =>
            {
    
                options.AddSupportedUICultures(LocalizationConstants.SupportedLanguages.Select(x => x.Code).ToArray());
                options.AddSupportedCultures(LocalizationConstants.SupportedLanguages.Select(x => x.Code).ToArray());
                options.DefaultRequestCulture = new RequestCulture(LocalizationConstants.DefaultLanguageCode);
                options.FallBackToParentUICultures = true;
            })
            .AddLocalization(options => options.ResourcesPath = LocalizationConstants.ResourcesPath);

        services.AddHangfire(configuration => configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseInMemoryStorage())
            .AddHangfireServer()
            .AddMvc();

        services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // Learner-facing API (Epic 3): anonymous, per-IP rate limiting - no learner UUID exists
        // yet at this pre-attempt discovery endpoint, so IP-based partitioning is the correct
        // interim scope (revisited once Story 3.3+ introduces a learner UUID).
        services.AddRateLimiter(options =>
        {
            // Per-client-IP partitioning (not AddFixedWindowLimiter, which would partition by the
            // constant "learner-api" policy-name string and share one global 60-req/min budget
            // across every caller). Rejections return 429 per the spec's I/O Matrix, overriding the
            // RateLimiterOptions default of 503.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("learner-api", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1)
                    }));
        });

        services.AddScoped<IApplicationHubWrapper, ServerHubWrapper>()
            .AddSignalR(options =>
            {
                options.MaximumReceiveMessageSize = 64 * 1024;
                options.AddFilter<UserContextHubFilter>();
            });
        
      
        
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        services.AddHealthChecks();


        services.AddHttpClient("ocr", (serviceProvider, c) =>
        {
            var aiSettings = serviceProvider.GetRequiredService<IAISettings>();
            c.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-preview-09-2025:generateContent");
            c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            c.DefaultRequestHeaders.Add("x-goog-api-key", aiSettings.GeminiApiKey);
           
        });
        services.AddScoped<LocalTimeOffset>();
        services.AddScoped<HubClient>();
        services
            .AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>()
            .AddScoped<LayoutService>()
            .AddScoped<DialogServiceHelper>()
            .AddScoped<IPermissionHelper, PermissionHelper>()
            .AddScoped<UserPermissionAssignmentService>()
            .AddScoped<RolePermissionAssignmentService>()
            .AddScoped<BlazorDownloadFileService>()
            .AddScoped<IUserPreferencesService, UserPreferencesService>()
            .AddScoped<IMenuService, MenuService>()
            .AddScoped<InMemoryNotificationService>()
            .AddScoped<INotificationService>(sp =>
            {
                var service = sp.GetRequiredService<InMemoryNotificationService>();
                service.Preload();
                return service;
            });


        return services;
    }

    /// <summary>
    /// Configures the server pipeline.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="config">The configuration.</param>
    /// <returns>The configured web application.</returns>
    public static WebApplication ConfigureServer(this WebApplication app, IConfiguration config)
    {
        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
        }
        else
        {
            // Unified exception handling: rely on AddExceptionHandler<GlobalExceptionHandler>() + ProblemDetails.
            // Removed the conventional "/Error" endpoint handler to avoid duplication/conflict.
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }
        
        // Single global exception handler registration (no path) to activate IExceptionHandler + ProblemDetails pipeline.
        app.UseExceptionHandler();
        app.UseStatusCodePagesWithRedirects("/404");
        app.UseForwardedHeaders();
        app.MapHealthChecks("/health");
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.UseHttpsRedirection();
        app.MapStaticAssets();
        

        if (!Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), @"Files")))
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), @"Files"));



        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), @"Files")),
            RequestPath = new PathString("/Files")
        });

        var localizationOptions = new RequestLocalizationOptions()
            .SetDefaultCulture(LocalizationConstants.DefaultLanguageCode)
            .AddSupportedCultures(LocalizationConstants.SupportedLanguages.Select(x => x.Code).ToArray())
            .AddSupportedUICultures(LocalizationConstants.SupportedLanguages.Select(x => x.Code).ToArray());

        // Remove AcceptLanguageHeaderRequestCultureProvider to prevent the browser's Accept-Language header from taking effect
        var acceptLanguageProvider = localizationOptions.RequestCultureProviders
            .OfType<AcceptLanguageHeaderRequestCultureProvider>()
            .FirstOrDefault();
        if (acceptLanguageProvider != null)
        {
            localizationOptions.RequestCultureProviders.Remove(acceptLanguageProvider);
        }
        app.UseRequestLocalization(localizationOptions);
    app.UseMiddleware<LocalizationCookiesMiddleware>();
        app.UseHangfireDashboard("/jobs", new DashboardOptions
        {
            Authorization = new[] { new HangfireDashboardAuthorizationFilter() },
            AsyncAuthorization = new[] { new HangfireDashboardAsyncAuthorizationFilter() }
        });
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
        app.MapHub<ServerHub>(ISignalRHub.Url);

        // Learner-facing /api/v1 surface (Epic 3): rate limiter must be wired before controllers
        // are mapped for [EnableRateLimiting] to take effect.
        app.UseRateLimiter();
        app.MapControllers();

        //QuestPDF License configuration
        Settings.License = LicenseType.Community;

        // Add additional endpoints required by the Identity /Account Razor components.
        app.MapAdditionalIdentityEndpoints();
        app.UseWebSockets(new WebSocketOptions()
        { // We obviously need this
            KeepAliveInterval = TimeSpan.FromSeconds(30), // Just in case
        });
       
        return app;
    }
}
