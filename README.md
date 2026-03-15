# Easshas Backend (DDD .NET 8)

A Domain-Driven Design ASP.NET Core Web API for:
- Admin auth + product management (POST/GET/PUT)
- User signup/signin with JWT in HttpOnly cookies
- Orders + Paystack payment init/verify
- Email notifications (AWS SES / Zoho SMTP)
- Real-time tracking with SignalR
- PostgreSQL persistence

## Projects
- src/Easshas.Domain — Entities, value objects, enums
- src/Easshas.Application — Service abstractions
- src/Easshas.Infrastructure — EF Core, Identity user, service implementations
- src/Easshas.WebApi — API endpoints, program startup, SignalR hub

---

## Local Development

### Prerequisites
- .NET SDK 8 (`dotnet --version` → `8.x.x`)
- Docker + Docker Compose (for the Postgres container)

### Quick start with Docker Compose

```bash
# 1. Clone the repository
git clone https://github.com/JosiahObajeOgakor/EesshasEcommerceBackendRepository.git
cd EesshasEcommerceBackendRepository

# 2. Create the .env file from the template and fill in the secrets
cp .env.example .env
# Edit .env – replace every CHANGE_ME_* placeholder with a real value

# 3. Start Postgres + API
docker compose up --build
```

API:     http://localhost:8080
Swagger: http://localhost:8080/swagger

### Quick start without Docker

```bash
# Start Postgres first (or adjust ConnectionStrings__Postgres in .env)
export ASPNETCORE_ENVIRONMENT=Development
dotnet run --project src/Easshas.WebApi
```

### Apply EF Core migrations

```bash
# Install the EF CLI tool (once)
dotnet tool install --global dotnet-ef

# From the repository root:
dotnet ef migrations add Initial \
  --project src/Easshas.Infrastructure \
  --startup-project src/Easshas.WebApi \
  --context Easshas.Infrastructure.Persistence.AppDbContext

dotnet ef database update \
  --project src/Easshas.Infrastructure \
  --startup-project src/Easshas.WebApi \
  --context Easshas.Infrastructure.Persistence.AppDbContext
```

---

## Deploying to Ubuntu Server (bare-metal / VPS)

### Prerequisites on the server
- Ubuntu 22.04 or 24.04
- A PostgreSQL instance accessible from the server
- A domain name pointing at the server's public IP (for TLS)

### Step 1 – Run the setup script (once)

```bash
git clone https://github.com/JosiahObajeOgakor/EesshasEcommerceBackendRepository.git
cd EesshasEcommerceBackendRepository
sudo bash scripts/deploy-ubuntu.sh
```

This installs the .NET 8 runtime, Nginx, and Certbot; creates the `easshas`
system user; registers the systemd service; and scaffolds the Nginx site.

### Step 2 – Fill in secrets

```bash
sudo nano /etc/easshas/environment
# Replace every CHANGE_ME_* value with your real credentials.
# The file is chmod 600 and owned by root, so it is not world-readable.
```

### Step 3 – Publish and copy the application

```bash
# On your build machine (or CI):
dotnet publish src/Easshas.WebApi -c Release -o /tmp/easshas-publish /p:UseAppHost=false

# Copy to the server (replace user@server with your SSH details):
rsync -avz /tmp/easshas-publish/ user@server:/opt/easshas/api/
ssh user@server "sudo chown -R easshas:easshas /opt/easshas/api"
```

### Step 4 – Apply database migrations

```bash
# From the repository root, pointing at the production DB:
ConnectionStrings__Postgres="Host=<db_host>;..." \
dotnet ef database update \
  --project src/Easshas.Infrastructure \
  --startup-project src/Easshas.WebApi \
  --context Easshas.Infrastructure.Persistence.AppDbContext
```

### Step 5 – Obtain a TLS certificate

```bash
sudo certbot --nginx -d api.eeshasgloss.com
```

Update `nginx/easshas.conf` (and the copy at `/etc/nginx/sites-available/easshas`)
with your real domain name before running Certbot.

### Step 6 – Start the service

```bash
sudo systemctl start easshas-api
sudo systemctl status easshas-api
```

### Automated deployments with GitHub Actions

The workflow in `.github/workflows/deploy.yml` builds, tests, and deploys on
every push to `main`. Set the following **GitHub Secrets** in your repository:

| Secret | Description |
|--------|-------------|
| `SSH_HOST` | Public IP or hostname of the Ubuntu server |
| `SSH_USER` | SSH username (e.g. `ubuntu`) |
| `SSH_PRIVATE_KEY` | Private SSH key authorised on the server |
| `SSH_PORT` | SSH port (optional, defaults to `22`) |

---

## Configuration

Key settings (set via environment variables or `/etc/easshas/environment`):

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__Postgres` | Full PostgreSQL connection string |
| `Jwt__Key` | 256-bit (32+ char) random secret for JWT signing |
| `Jwt__Issuer` / `Jwt__Audience` | Token issuer/audience (your domain) |
| `Paystack__SecretKey` | Live secret key from Paystack dashboard |
| `Email__Smtp__Password` | SMTP password for the configured email provider |
| `Email__From` / `Email__Admin` | Sender / admin email addresses |
| `AWS__Region` / `AWS__Bucket` | S3 bucket for file uploads |
| `AWS__AccessKey` / `AWS__SecretKey` | AWS IAM credentials |
| `Admin__Username` / `Admin__Password` | Seed admin credentials (first boot only) |

Copy `.env.example` to `.env` (local) or `/etc/easshas/environment` (server)
and replace every `CHANGE_ME_*` placeholder before starting the application.

---

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
  "description": "Description",
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
  - Anonymous polling endpoint for the frontend "Processing" page; returns `{ orderId, status, totalAmount, currency, createdAt, paidAt }`.
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
- Ensure HTTPS is enforced in production; cookies are Secure and SameSite=None by default.
- Use Paystack webhooks in addition to callback for robust reconciliation.

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
