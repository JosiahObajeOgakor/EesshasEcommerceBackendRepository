# Easshas Backend (DDD .NET 8)

A Domain-Driven Design ASP.NET Core Web API for:
- Admin auth + product management (POST/GET/PUT)
- User signup/signin with JWT in HttpOnly cookies
- Orders + Paystack payment init/verify
- Email notifications (AWS SES)
- Real-time tracking with SignalR
- PostgreSQL persistence

## Projects
- src/Easshas.Domain — Entities, value objects, enums
- src/Easshas.Application — Service abstractions
- src/Easshas.Infrastructure — EF Core, Identity user, service implementations
- src/Easshas.WebApi — API endpoints, program startup, SignalR hub

## Local Development

Prereqs:
- .NET SDK 8
- Docker (for Postgres) or local Postgres

Run with Docker Compose:
```bash
cd C:/Users/Josiah.Obaje/source/repos/JosiahCourses/easshasbackend
set PAYSTACK_TEST_SECRET_KEY=sk_test_xxx
docker compose up --build
```
API: http://localhost:8080

Or run with dotnet:
```bash
cd C:/Users/Josiah.Obaje/source/repos/JosiahCourses/easshasbackend
# start postgres first or adjust ConnectionStrings__Postgres env var
set ASPNETCORE_ENVIRONMENT=Development
cd src/Easshas.WebApi
dotnet run
```

Apply EF migrations (suggested):
```bash
# install EF tools
 dotnet tool install --global dotnet-ef
cd src/Easshas.WebApi
# add migration
 dotnet ef migrations add Initial --project ..\Easshas.Infrastructure --startup-project . --context Easshas.Infrastructure.Persistence.AppDbContext
# update db
 dotnet ef database update --project ..\Easshas.Infrastructure --startup-project . --context Easshas.Infrastructure.Persistence.AppDbContext
```

## Configuration

Key settings (appsettings.json):
- ConnectionStrings:Postgres
- Jwt: Key (256-bit), Issuer, Audience, CookieName
- Paystack: SecretKey (live in prod)
- Paystack:Subscriptions: SecretKey (separate secret for admin monthly plan)
- Email: From, Admin
- Email: SesSecretName (optional Secrets Manager name containing { AccessKeyId, SecretAccessKey, Region })
- WhatsApp: Enabled, AdminPhone (and provider credentials when added)
- AWS: Region
- Admin: Username, Password (for seeding)

## Auth Endpoints
- POST /api/auth/signup { username, password, email }
- POST /api/auth/signin { username, password } → sets HttpOnly Secure cookie `access_token`
- POST /api/auth/signout → deletes cookie

## Product Endpoints
- POST /api/admin/products (Admin)
- GET /api/admin/products/{id} (Public)
- PUT /api/admin/products/{id} (Admin)

Example Product Payload:
```
{
  "name": "Blush Choco",
  "price": 4500,
  "category": "lipgloss",
  "description": "Decription:",
  "brandName": "Brand Name"
}
```

## Order + Payment
- POST /api/orders
Request:
```
{
  "productId": "<guid>",
  "quantity": 1,
  "fullName": "Jane Doe",
  "line1": "123 Street",
  "line2": null,
  "city": "Lagos",
  "state": "LA",
  "country": "NG",
  "postalCode": "100001",
  "phoneNumber": "+2348000000000",
  "expectedDeliveryDate": "2026-01-30",
  "emailForPayment": "buyer@example.com",
  "callbackUrl": "https://yourapp/callback"  
}
```
Response: `{ orderId, amount, currency, authorizationUrl, reference }`

- GET /api/payments/paystack/callback?reference=...&trxref=...&redirect=https://yourapp/payment/result
  - Verifies payment, marks order paid (deducts inventory), and sends emails/WhatsApp.
  - Redirects to `redirect` (or configured Payment.ReturnUrlSuccess/Failure) with `status` and `reference` query params.
- POST /api/payments/verify { reference, redirect? }
  - Frontend-driven verification when using pure frontend redirects; marks paid and sends notifications; optional redirect.
- GET /api/orders/status?reference=...
  - Anonymous polling endpoint for the frontend “Processing” page; returns `{ orderId, status, totalAmount, currency, createdAt, paidAt }`.
- POST /api/webhooks/paystack (set in Paystack Dashboard)
  - Validates `x-paystack-signature` (HMAC-SHA512 with your secret), processes `charge.success` idempotently, marks order paid and emails buyer/admin.
  - Legacy alias also supported: POST /api/payments/webhook

## Contacts (Validation)
- Unified endpoint: `/api/contacts`
  - POST body `{ phone? , email? }` (provide exactly one)
  - GET query `?phone=...` or `?email=...` (provide exactly one)
  - PATCH body `{ phone? , email? }` (provide exactly one)
  - DELETE query `?phone=...` or `?email=...` (provide exactly one)
- Rules:
  - Phone: E.164 format only (e.g., `+2348012345678`)
  - Email: Valid format, recognized provider domain, and not disposable (configurable via `Email.RecognizedProviders` and `Email.DisposableDomains`).

## Real-time Tracking
- Client connects to SignalR hub `/hubs/tracking`
- Join group: `connection.invoke("JoinOrderGroup", orderId)`
- Admin updates location: POST /api/tracking { orderId, latitude, longitude }
- Clients receive `location` events

## WhatsApp Notifications (Premium)
- Admin subscribes via POST /api/admin/subscriptions/init (NGN 50k/month). Optional verify via POST /api/admin/subscriptions/verify?reference=...
- On order payment, if WhatsApp.Enabled=true and subscription active, notifier sends messages to user/admin (provider integration stubbed).

## Security Notes
- Never use public (pk_) key as SecretKey. Use your Paystack live secret key (sk_live_...).
- Configure webhooks with your server URL and verify x-paystack-signature.
- Prefer webhook as canonical source of truth; callback/verify are user-experience helpers.

## AWS Deployment (Overview)
- Build Docker image for WebApi
- Push to ECR
- Provision RDS PostgreSQL
- Store secrets in AWS Secrets Manager (DB, Paystack, JWT key)
- Deploy on ECS Fargate or Elastic Beanstalk (Docker)
- Configure ALB with HTTPS, forward to container port 8080
- Set env vars:
  - ConnectionStrings__Postgres
  - Jwt__Key/Issuer/Audience/CookieName
  - Paystack__SecretKey
  - Email__From, Email__Admin
  - AWS__Region
  - Admin__Username, Admin__Password, Admin__Email (optional)

Quick ECS Fargate deployment script:
```powershell
./scripts/deploy_aws_fargate.ps1 \
  -AccountId <aws-account-id> \
  -Region us-east-1 \
  -TaskExecutionRoleArn arn:aws:iam::<aws-account-id>:role/ecsTaskExecutionRole \
  -Subnets subnet-abc123,subnet-def456 \
  -SecurityGroups sg-abc123 \
  -AssignPublicIp
```

GitHub Actions deployment (no local AWS CLI needed):
- Workflow: `.github/workflows/deploy-ecs.yml`
- Trigger: push to `main` or manual run (`workflow_dispatch`)
- Required repository variables:
  - `AWS_REGION`
  - `ECR_REPOSITORY`
  - `ECS_CLUSTER`
  - `ECS_SERVICE`
  - `ECS_TASK_FAMILY`
  - `ECS_EXECUTION_ROLE_ARN`
  - `ECS_TASK_ROLE_ARN` (optional)
  - `ECS_SUBNETS` (comma-separated)
  - `ECS_SECURITY_GROUPS` (comma-separated)
  - `ECS_ASSIGN_PUBLIC_IP` (`ENABLED` or `DISABLED`)
  - `CONTAINER_NAME` (example: `easshas-api`)
  - `APP_CPU` (optional, default `512`)
  - `APP_MEMORY` (optional, default `1024`)
- Required repository secrets:
  - `AWS_ACCESS_KEY_ID`
  - `AWS_SECRET_ACCESS_KEY`
  - `CONNECTIONSTRINGS_POSTGRES`
  - `JWT_KEY`
  - `JWT_ISSUER`
  - `JWT_AUDIENCE`
  - `PAYSTACK_SECRET_KEY`
  - `OPENAI_API_KEY`
  - `AWS_REGION_APP`
  - `AWS_ACCESS_KEY_APP`
  - `AWS_SECRET_KEY_APP`
  - `EMAIL_FROM`
  - `EMAIL_ADMIN`

## Notes
- Ensure HTTPS is enforced in production; cookies are Secure and SameSite=None by default.
- Use Paystack webhooks in addition to callback for robust reconciliation.
- Consider adding refresh tokens for long-lived sessions if needed.
