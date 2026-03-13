using System;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Easshas.WebApi.StartupExtensions
{
    public class SwaggerAuthOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var path = context.ApiDescription.RelativePath ?? string.Empty;
            var isSigninRoute = path.Contains("auth/signin/admin", StringComparison.OrdinalIgnoreCase)
                                || path.Contains("auth/signin/user", StringComparison.OrdinalIgnoreCase);

            // Don't attach security to signin endpoints
            if (isSigninRoute)
            {
                operation.Security?.Clear();
                return;
            }

            // Contacts endpoints use Basic-style passkey
            var isContacts = path.StartsWith("api/contacts", StringComparison.OrdinalIgnoreCase);

            operation.Security ??= new System.Collections.Generic.List<OpenApiSecurityRequirement>();
            if (isContacts)
            {
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "ContactsBasic"
                        }
                    }] = Array.Empty<string>()
                });
            }
            else
            {
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    }] = Array.Empty<string>()
                });
            }
        }
    }
}
