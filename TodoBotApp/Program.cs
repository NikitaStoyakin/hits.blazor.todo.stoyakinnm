using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TodoBotApp.Components;
using TodoBotApp.Components.Account;
using TodoBotApp.Data;
using TodoBotApp.Services; 

namespace TodoBotApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddScoped<IdentityUserAccessor>();
            builder.Services.AddScoped<IdentityRedirectManager>();
            builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

            builder.Services.AddScoped<IBotService, BotService>();
            builder.Services.AddScoped<IExpertService, ExpertService>();

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
                .AddIdentityCookies();

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddIdentityCore<ApplicationUser>(options => 
            {
                options.SignIn.RequireConfirmedAccount = !builder.Environment.IsDevelopment();
            })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAntiforgery();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.MapAdditionalIdentityEndpoints();

            await InitializeRolesAsync(app.Services);
            if (app.Environment.IsDevelopment())
            {
                await InitializeTestExpertAsync(app.Services);
            }

            app.Run();
        }

        private static async Task InitializeRolesAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            var roles = new[] { "Expert" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }

        private static async Task InitializeTestExpertAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            const string expertEmail = "expert@test.com";
            const string expertPassword = "Expert123!";

            var expertUser = await userManager.FindByEmailAsync(expertEmail);
            
            if (expertUser == null)
            {
                expertUser = new ApplicationUser
                {
                    UserName = expertEmail,
                    Email = expertEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(expertUser, expertPassword);
                
                if (result.Succeeded)
                {
                    var expertRole = await roleManager.FindByNameAsync("Expert");
                    if (expertRole != null)
                    {
                        await userManager.AddToRoleAsync(expertUser, "Expert");
                    }
                }
            }
            else
            {
                var isInRole = await userManager.IsInRoleAsync(expertUser, "Expert");
                if (!isInRole)
                {
                    var expertRole = await roleManager.FindByNameAsync("Expert");
                    if (expertRole != null)
                    {
                        await userManager.AddToRoleAsync(expertUser, "Expert");
                    }
                }
            }
        }
    }
}
