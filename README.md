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
# from the repository root
cp .env.example .env   # fill in your values
docker compose up --build
```
API: http://localhost:8080

Or run with dotnet:
```bash
# from the repository root
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

## AWS Deployment

The repository ships with a GitHub Actions workflow (`.github/workflows/deploy-aws.yml`) that
automatically **builds, tests, and deploys** the API to AWS ECS Fargate on every push to `main`.

### Prerequisites

| Resource | Notes |
|----------|-------|
| AWS Account | `us-east-2` region (matches existing RDS) |
| Amazon ECR repository | Stores Docker images |
| Amazon ECS Cluster + Service | Runs the Fargate containers |
| AWS RDS PostgreSQL | Already configured in `appsettings.Production.json` |
| AWS Secrets Manager | Stores all sensitive configuration |
| IAM roles | `ecsTaskExecutionRole` (pull from ECR + read Secrets Manager) and `ecsTaskRole` (S3 access) |

### One-time setup

**1. Create the ECS task definition**

Edit `ecs-task-definition.json`, replace every `ACCOUNT_ID` placeholder with your AWS account ID,
update the Secrets Manager ARNs if needed, then register the task definition:
```bash
aws ecs register-task-definition --cli-input-json file://ecs-task-definition.json
```

**2. Create the CloudWatch log group**
```bash
aws logs create-log-group --log-group-name /ecs/easshas-api --region us-east-2
```

**3. Store secrets in AWS Secrets Manager**

The task definition references the following secrets (create each as a JSON key/value in Secrets Manager):
```bash
# PostgreSQL connection string
aws secretsmanager create-secret --name prod/easshas/postgres \
  --secret-string '{"connection_string":"Host=...;Port=5432;Database=postgres;..."}'

# JWT signing key
aws secretsmanager create-secret --name prod/easshas/jwt \
  --secret-string '{"key":"<256-bit-secret>"}'

# Paystack API keys
aws secretsmanager create-secret --name prod/easshas/paystack \
  --secret-string '{"secret_key":"sk_live_...","public_key":"pk_live_..."}'

# AWS S3 / SES credentials
aws secretsmanager create-secret --name prod/easshas/aws \
  --secret-string '{"access_key":"AKIA...","secret_key":"..."}'

# Email (SES / Zoho SMTP)
aws secretsmanager create-secret --name prod/SES \
  --secret-string '{"from":"no-reply@your-domain.com","smtp_password":"..."}'

# WhatsApp (Meta)
aws secretsmanager create-secret --name prod/easshas/whatsapp \
  --secret-string '{"access_token":"...","phone_number_id":"..."}'

# OpenAI
aws secretsmanager create-secret --name prod/easshas/openai \
  --secret-string '{"api_key":"sk-..."}'

# Contacts API basic auth
aws secretsmanager create-secret --name prod/easshas/contacts \
  --secret-string '{"password":"<contacts-api-password>"}'
```

**4. Add GitHub repository secrets**

In your GitHub repository → *Settings → Secrets and variables → Actions*, add:

| Secret | Value |
|--------|-------|
| `AWS_ACCESS_KEY_ID` | IAM user access key (CI/CD deploy user) |
| `AWS_SECRET_ACCESS_KEY` | IAM user secret key |
| `ECR_REPOSITORY` | ECR repository name (e.g. `easshas-api`) |
| `ECS_CLUSTER` | ECS cluster name (e.g. `easshas-cluster`) |
| `ECS_SERVICE` | ECS service name (e.g. `easshas-api`) |
| `CONTAINER_NAME` | Container name in task definition (`easshas-api`) |

**5. Configure the ALB and ECS Service**

- Create an Application Load Balancer (HTTPS, port 443) forwarding to the container on port **8080**.
- Add your production domain to the `Cors__Origins` environment variable in the task definition.
- Set `ASPNETCORE_ENVIRONMENT=Production` (already in the task definition template).

### CI/CD flow

```
Push to main
  → GitHub Actions: dotnet test
  → Docker build (from repo root, Dockerfile at src/Easshas.WebApi/Dockerfile)
  → Push image to ECR (tagged with git SHA + latest)
  → Download current ECS task definition
  → Render new task definition with updated image
  → Deploy to ECS service (waits for stability)
```

### Local Docker build

The Dockerfile requires the **repository root** as the build context (it copies the entire solution):
```bash
# from the repository root
docker build -f src/Easshas.WebApi/Dockerfile -t easshas-api .
```

Or use Docker Compose (also runs a local Postgres):
```bash
cp .env.example .env   # fill in your values
docker compose up --build
```

## Notes
- Ensure HTTPS is enforced in production; cookies are Secure and SameSite=None by default.
- Use Paystack webhooks in addition to callback for robust reconciliation.
- Consider adding refresh tokens for long-lived sessions if needed.
