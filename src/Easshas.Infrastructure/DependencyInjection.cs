using System;
using Easshas.Application.Abstractions;
using Easshas.Infrastructure.Configuration;
using Easshas.Infrastructure.Identity;
using Easshas.Infrastructure.Persistence;
using Easshas.Infrastructure.RealTime;
using Easshas.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Easshas.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<JwtOptions>(configuration.GetSection("Jwt"));

            services.AddDbContext<AppDbContext>(options =>
            {
                var useInMemory = configuration.GetValue<bool>("App:UseInMemory");
                if (useInMemory)
                {
                    options.UseInMemoryDatabase("easshas_dev_mem");
                }
                else
                {
                    options.UseNpgsql(configuration.GetConnectionString("Postgres"));
                }
            });

            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<IOrderService, OrderService>();
            services.AddScoped<IJwtTokenService, JwtTokenService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IPaystackService, PaystackService>();
            var emailProvider = configuration["Email:Provider"] ?? string.Empty;
            Console.WriteLine($"DI: Detected Email Provider: '{emailProvider}'");
            if (string.Equals(emailProvider, "Zoho", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("DI: Registering ZohoEmailSender");
                services.AddScoped<IEmailSender, ZohoEmailSender>();
            }
            else
            {
                Console.WriteLine("DI: Registering EmailSender");
                services.AddScoped<IEmailSender, EmailSender>();
            }
            services.AddScoped<IAdminSubscriptionService, AdminSubscriptionService>();
            services.AddScoped<IWhatsAppNotifier, WhatsAppNotifier>();
            services.AddScoped<IChatSessionService, ChatSessionService>();
            services.AddScoped<ICommerceAgent, CommerceAgent>();
            services.AddScoped<IConversationService, ConversationService>();
            services.AddScoped<IAgenticService, AgenticService>();
            // LLM + RAG services
            services.AddHttpClient("OpenAI");
            services.AddScoped<IPlanner, OpenAiPlanner>();
            services.AddScoped<Easshas.Application.Abstractions.IIntentClassifier, OpenAiIntentClassifier>();
            services.AddScoped<IEmbeddingService, OpenAiEmbeddingService>();
            services.AddScoped<IVectorStore, PgVectorStore>();
            services.AddScoped<IRefreshTokenService, RefreshTokenService>();
            services.AddSingleton<IEmailValidationService, EmailValidationService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IContactMessageService, ContactMessageService>();
                services.AddScoped<ICartService, CartService>();
            services.AddScoped<IPasswordResetService, PasswordResetService>();
                // Removed AgentChatService (duplicate lightweight agent); use ConversationService (`api/agentic-chat`) as primary

            return services;
        }
    }
}
