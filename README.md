# Easshas Backend (DDD .NET 8)

A Domain-Driven Design ASP.NET Core Web API for:
- Admin auth + product management (POST/GET/PUT)
- User signup/signin with JWT in HttpOnly cookies
- Orders + Paystack payment init/verify
- Email notifications (Zoho SMTP / AWS SES)
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

Run with Docker Compose (local):
```bash
# From the repository root
cp .env.example .env          # then fill in real values
docker compose up --build
```
API: http://localhost:8080

Or run with dotnet:
```bash
# start postgres first or adjust ConnectionStrings__Postgres env var
export ASPNETCORE_ENVIRONMENT=Development
cd src/Easshas.WebApi
dotnet run
```

Apply EF migrations:
```bash
# Install EF tools (once)
dotnet tool install --global dotnet-ef

# From the repository root
dotnet ef migrations add Initial \
  --project src/Easshas.Infrastructure \
  --startup-project src/Easshas.WebApi \
  --context Easshas.Infrastructure.Persistence.AppDbContext

dotnet ef database update \
  --project src/Easshas.Infrastructure \
  --startup-project src/Easshas.WebApi \
  --context Easshas.Infrastructure.Persistence.AppDbContext
```

## Ubuntu Server Deployment

### Prerequisites on the server
```bash
# Install Docker Engine
sudo apt-get update
sudo apt-get install -y ca-certificates curl gnupg
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg \
  | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] \
  https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
  | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io \
  docker-buildx-plugin docker-compose-plugin

# Allow running docker without sudo (re-login after)
sudo usermod -aG docker $USER

# Install nginx + certbot for HTTPS
sudo apt-get install -y nginx certbot python3-certbot-nginx
```

### Deploy with the provided script
```bash
# Clone the repo on the server
git clone https://github.com/JosiahObajeOgakor/EesshasEcommerceBackendRepository.git
cd EesshasEcommerceBackendRepository

# Create your .env file and fill in all CHANGE_ME values
cp .env.example .env
nano .env

# Run the deploy script
bash deploy.sh
```

The script will:
1. Validate that `.env` exists and warn about un-replaced placeholders.
2. Build the Docker image and start `db` + `api` containers.
3. Wait for PostgreSQL to be healthy before the API connects.
4. Optionally install the nginx reverse proxy config and obtain a TLS certificate via Certbot.

### Manual nginx setup
```bash
sudo cp nginx/easshas.conf /etc/nginx/sites-available/easshas
sudo sed -i 's/your-domain.com/eeshasgloss.com/g' /etc/nginx/sites-available/easshas
sudo ln -s /etc/nginx/sites-available/easshas /etc/nginx/sites-enabled/easshas
sudo nginx -t && sudo systemctl reload nginx
sudo certbot --nginx -d eeshasgloss.com
```

### Updating the deployment
```bash
git pull --ff-only
docker compose up --build -d
```

## Configuration

All secrets **must** be provided via environment variables (`.env` file) — never commit real secrets to source control.
See `.env.example` for the full list. Key variables:

| Variable | Description |
|---|---|
| `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` | PostgreSQL container credentials |
| `CONNECTIONSTRINGS_POSTGRES` | Full Npgsql connection string for the API |
| `JWT_KEY` | 256-bit secret key for JWT signing |
| `JWT_ISSUER` / `JWT_AUDIENCE` | Your domain (e.g. `https://eeshasgloss.com`) |
| `PAYSTACK_SECRET_KEY` | Live Paystack secret (`sk_live_...`) |
| `EMAIL_FROM` / `EMAIL_ADMIN` | Sender addresses |
| `EMAIL_SMTP_*` | SMTP host/port/username/password |
| `CORS_ORIGIN_0` | Primary allowed CORS origin |
| `AWS_REGION` / `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` | AWS credentials |
| `OPENAI_API_KEY` | OpenAI API key for AI features |

## Auth Endpoints
- POST /api/auth/signup { username, password, email }
- POST /api/auth/signin { username, password } → sets HttpOnly Secure cookie `access_token`
- POST /api/auth/signout → deletes cookie

## Product Endpoints
- POST /api/admin/products (Admin)
- GET /api/admin/products/{id} (Public)
- PUT /api/admin/products/{id} (Admin)

Example Product Payload:
```json
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
```json
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
- POST /api/payments/verify { reference, redirect? }
- GET /api/orders/status?reference=...
- POST /api/webhooks/paystack — validates `x-paystack-signature`, processes `charge.success`

## Contacts (Validation)
- Unified endpoint: `/api/contacts`
  - POST/PATCH body `{ phone?, email? }` — provide exactly one
  - GET/DELETE query `?phone=...` or `?email=...` — provide exactly one
- Phone: E.164 format only (e.g. `+2348012345678`)
- Email: valid format, recognised provider domain, not disposable

## Real-time Tracking
- Client connects to SignalR hub `/hubs/tracking`
- Join group: `connection.invoke("JoinOrderGroup", orderId)`
- Admin updates location: POST /api/tracking { orderId, latitude, longitude }
- Clients receive `location` events

## WhatsApp Notifications (Premium)
- Admin subscribes via POST /api/admin/subscriptions/init (NGN 50k/month)
- On order payment, if `WhatsApp__Enabled=true` and subscription active, notifier sends messages

## Security Notes
- Never use the public key (`pk_`) as the secret key. Use `sk_live_...`.
- Configure Paystack webhooks and verify `x-paystack-signature`.
- All secrets are injected via environment variables — see `.env.example`.
- HTTPS is enforced in production; cookies are Secure and SameSite=None.

## AWS Deployment (Overview)
- Build Docker image and push to ECR
- Provision RDS PostgreSQL
- Store secrets in AWS Secrets Manager
- Deploy on ECS Fargate or Elastic Beanstalk (Docker)
- Configure ALB with HTTPS, forward to container port 8080
