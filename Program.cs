using E_Commerce.BackgroundJops;
using E_Commerce.Context;
using E_Commerce.DtoModels;
using E_Commerce.DtoModels.Responses;
using E_Commerce.ErrorHnadling;
using E_Commerce.Interfaces;
using E_Commerce.Mappings;
using E_Commerce.Middleware;
using E_Commerce.Models;
using E_Commerce.Repository;
using E_Commerce.Services;
using E_Commerce.Services.AccountServices;
using E_Commerce.Services.AccountServices.Shared;
using E_Commerce.Services.AdminOpreationServices;
using E_Commerce.Services.Cache;
using E_Commerce.Services.CategoryServcies;
using E_Commerce.Services.EmailServices;

using E_Commerce.Services.ProductInventoryServices;
using E_Commerce.Services.WareHouseServices;
using E_Commerce.Services.CustomerAddress;
using E_Commerce.Services.Order;
using E_Commerce.Services.Collection;
using E_Commerce.UOW;
using Hangfire;
using Hangfire.MySql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MySqlConnector;
using Newtonsoft.Json;
using Scalar.AspNetCore;
using Serilog;
using Serilog;
using Serilog.AspNetCore;
using StackExchange.Redis;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Routing;
using E_Commerce.Services.Discount;
using E_Commerce.Services.AccountServices.Registration;
using E_Commerce.Services.AccountServices.Password;
using E_Commerce.Services.AccountServices.Profile;
using E_Commerce.Services.AccountServices.AccountManagement;
using E_Commerce.Services.AccountServices.Authentication;
using E_Commerce.Services.ProductServices;
using E_Commerce.Services.SubCategoryServices;
using E_Commerce.Services.BackgroundServices;
using E_Commerce.Services.UserOpreationServices;
using E_Commerce.Services.CartServices;
using E_Commerce.Services.PayMobServices;
using E_Commerce.Services.PaymentProccessor;

namespace E_Commerce
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder
                .Services.AddControllers()
                .AddNewtonsoftJson(options =>
                {
                    options.SerializerSettings.ReferenceLoopHandling =
                        ReferenceLoopHandling.Serialize;
                })
                .ConfigureApiBehaviorOptions(options =>
                    options.SuppressModelStateInvalidFilter = true
                );
			Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
			 .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day)
			 .CreateLogger();

			builder.Host.UseSerilog();
			builder.Services.AddHttpContextAccessor();

			builder.Services.AddTransient<ICategoryLinkBuilder, CategoryLinkBuilder>();
            builder.Services.AddTransient<IProductLinkBuilder, ProductLinkBuilder>();
			builder.Services.AddTransient<IAccountLinkBuilder, AccountLinkBuilder>();
			builder.Services.AddTransient<IWareHouseLinkBuilder, WareHouseLinkBuilder>();
            builder
                .Services.AddIdentity<Customer, IdentityRole>(options =>
                {
                    var passwordPolicy = builder.Configuration.GetSection("Security:PasswordPolicy");
                    options.Password.RequireDigit = passwordPolicy.GetValue<bool>("RequireDigit", true);
                    options.Password.RequireLowercase = passwordPolicy.GetValue<bool>("RequireLowercase", true);
                    options.Password.RequireUppercase = passwordPolicy.GetValue<bool>("RequireUppercase", true);
                    options.Password.RequireNonAlphanumeric = passwordPolicy.GetValue<bool>("RequireNonAlphanumeric", true);
                    options.Password.RequiredLength = passwordPolicy.GetValue<int>("RequiredLength", 8);
                    options.Password.RequiredUniqueChars = passwordPolicy.GetValue<int>("RequiredUniqueChars", 4);
                    var lockoutPolicy = builder.Configuration.GetSection("Security:LockoutPolicy");
                    
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(
                        lockoutPolicy.GetValue<int>("LockoutDurationMinutes", 15)
                    );
                    options.Lockout.MaxFailedAccessAttempts = lockoutPolicy.GetValue<int>("MaxFailedAttempts", 5);
                    options.Lockout.AllowedForNewUsers = true;
                    options.User.RequireUniqueEmail = true;
                    options.SignIn.RequireConfirmedEmail = true;
                })
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();
            builder.Services.AddScoped<ITokenService, TokenService>();
            builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
            builder.Services.AddScoped<IImagesServices, ImagesServices>();
            builder.Services.AddTransient<IErrorNotificationService, ErrorNotificationService>();
            builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
            builder.Services.AddScoped<IImageRepository, ImageRepository>();
            builder.Services.AddScoped<ICategoryServices, CategoryServices>();
            builder.Services.AddScoped<IWareHouseRepository, WareHouseRepository>();
            builder.Services.AddScoped<IProductRepository, ProductRepository>();
            builder.Services.AddScoped<IProductVariantRepository, ProductVariantRepository>();
            builder.Services.AddScoped<IProductInventoryRepository, ProductInventoryRepository>();
            builder.Services.AddScoped<IAdminOpreationServices, AdminOpreationServices>();
            builder.Services.AddScoped<IUserOpreationServices, UserOpreationServices>();
            builder.Services.AddScoped<IWareHouseServices, WareHouseServices>();
            builder.Services.AddScoped<ICartRepository, CartRepository>();
            builder.Services.AddScoped<ICartServices, CartServices>();
			builder.Services.AddScoped<IPaymentMethodsServices, PaymentMethodsServices>();
			builder.Services.AddScoped<IPaymentProcessor, PayMobServices>();

			builder.Services.AddScoped<IOrderRepository, OrderRepository>();
            builder.Services.AddScoped<IOrderServices, OrderServices>();
            builder.Services.AddScoped<ICollectionRepository, CollectionRepository>();
            builder.Services.AddScoped<ICollectionServices, CollectionServices>();
            builder.Services.AddScoped<ICustomerAddressRepository, CustomerAddressRepository>();
            builder.Services.AddScoped<ICustomerAddressServices, CustomerAddressServices>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddScoped<IPaymentProvidersServices, PaymentProvidersServices>();
            builder.Services.AddScoped<IPaymentServices, PaymentServices>();

            builder.Services.AddScoped(typeof(IRepository<>), typeof(MainRepository<>));
            builder.Services.AddScoped<IAccountServices, AccountServices>();
            builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
            builder.Services.AddScoped<IRegistrationService, RegistrationService>();
            builder.Services.AddScoped<IPasswordService, PasswordService>();
            builder.Services.AddScoped<IProfileService, ProfileService>();
            builder.Services.AddScoped<IAccountManagementService, AccountManagementService>();
            builder.Services.AddScoped<IProductCatalogService, ProductCatalogService>();
            builder.Services.AddScoped<IProductSearchService, ProductSearchService>();
            builder.Services.AddScoped<IProductImageService, ProductImageService>();
            builder.Services.AddScoped<IProductVariantService, ProductVariantService>();
            builder.Services.AddScoped<IProductDiscountService, ProductDiscountService>();
            builder.Services.AddScoped<IProductInventoryService, Services.ProductInventoryServices.ProductInventoryService>();
            builder.Services.AddScoped<IProductsServices, ProductsServices>();
            builder.Services.AddScoped<IDiscountService, DiscountService>();
            builder.Services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();
            builder.Services.AddAutoMapper(typeof(MappingProfile));
			builder.Services.AddTransient<IEmailSender, EmailSender>();
			builder.Services.AddScoped<IAccountEmailService, AccountEmailService>();
			builder.Services.AddScoped<ErrorNotificationService>();
			builder.Services.AddScoped<CategoryCleanupService>();
			builder.Services.AddScoped<ISubCategoryServices, SubCategoryServices>();
			builder.Services.AddScoped<ISubCategoryRepository, SubCategoryRepository>();
			builder.Services.AddScoped<IAdminOpreationServices, AdminOpreationServices>();
			builder.Services.AddTransient<ISubCategoryLinkBuilder, SubCategoryLinkBuilder>();
            builder.Services.AddResponseCaching();
			builder.Services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect("Localhost:6379")
            );
            builder.Services.AddSingleton<ICacheManager, CacheManager>();
            builder.Services.AddDbContext<AppDbContext>(
                (provider, options) =>
                {
                    options.UseMySql(
                        builder.Configuration.GetConnectionString("MyConnectionMySql"),
                        new MySqlServerVersion(new Version(8, 0, 21))
                    );
                }
            );
            builder.Services.AddHangfire(config =>
                config.UseStorage(
                    new MySqlStorage(
                        builder.Configuration.GetConnectionString("MyConnectionMySql"),
                        new MySqlStorageOptions
                        {
                            TablesPrefix = "Hangfire_",
                            QueuePollInterval = TimeSpan.FromSeconds(10),
                        }
                    )
                )
            );
			builder.Services.AddHangfireServer();
			builder.Services.AddCors(options =>
            {
                options.AddPolicy(
                    "MyPolicy",
                    Options =>
                    {
                        Options.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
                    }
                );
            });
            builder.Services.AddRateLimiter(async options =>
            {
                
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
                            Window = TimeSpan.FromMinutes(1),
                            AutoReplenishment = true
                        }));

                options.AddPolicy("login", context =>
                    RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = 15,
                            SegmentsPerWindow= 3,
                            Window = TimeSpan.FromMinutes(1),
                            AutoReplenishment = true
                        }));

             
                options.AddPolicy("register", context =>
                    RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = 6,
                            SegmentsPerWindow = 3,
							Window = TimeSpan.FromMinutes(1),
                            AutoReplenishment = true
                        }));

                options.AddPolicy("reset", context =>
                    RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = 6,
                            SegmentsPerWindow = 3,
							Window = TimeSpan.FromMinutes(1),
                            AutoReplenishment = true
                        }));
                options.OnRejected= async (context,token) =>
                {
                    context.HttpContext.Response.StatusCode = 429;
                    context.HttpContext.Response.ContentType = "application/json";

                    var response = ApiResponse<string>.CreateErrorResponse("Error", new ErrorResponse("Requests","Too many request"),429);
                    await context.HttpContext.Response.WriteAsync(
                        JsonConvert.SerializeObject(response),
                        token
                    );

				};
			});
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });

                var securityScheme = new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Description = "Enter JWT Bearer token",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    },
                };

                c.AddSecurityDefinition("Bearer", securityScheme);

                var securityRequirement = new OpenApiSecurityRequirement
                {
                    { securityScheme, new[] { "Bearer" } },
                };

                c.AddSecurityRequirement(securityRequirement);
            });
            builder
                .Services.AddAuthentication(options =>
                {
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.SaveToken = true; 
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidateAudience = true,
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(
                                builder.Configuration["Jwt:Key"]
                                    ?? throw new Exception("Key is missing")
                            )
                        ),
                        ValidateLifetime = true,
                    };
                });

            var app = builder.Build();
            app.UseCors("MyPolicy");
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseHangfireDashboard("/hangfire");
     

			using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var dbContext = services.GetRequiredService<AppDbContext>();

                dbContext.Database.Migrate();
                await DataSeeder.SeedDataAsync(services);
                var categoryCleanupService =
                    scope.ServiceProvider.GetRequiredService<CategoryCleanupService>();
                RecurringJob.AddOrUpdate(
                    "Clean-Category",
                    () => categoryCleanupService.DeleteOldCategories(),
                    Cron.Daily
                );
            }

            app.UseRouting();
            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseUserAuthentication();
			app.UseMiddleware<SecurityStampMiddleware>();
            app.UseStaticFiles();
            app.UseResponseCaching();
			app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
			app.Run();
        }
    }
}
