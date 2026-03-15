#!/usr/bin/env bash
# deploy.sh — Deploy Easshas backend to an Ubuntu server using Docker Compose
#
# Usage:
#   1. Copy this script to the server alongside the repository.
#   2. Create a .env file in the repository root (copy from .env.example and fill in values).
#   3. Run:  bash deploy.sh
#
# Prerequisites on the Ubuntu server:
#   - Docker Engine  (https://docs.docker.com/engine/install/ubuntu/)
#   - Docker Compose plugin (included with Docker Engine >= 23)
#   - Nginx + Certbot (for HTTPS reverse proxy)
#       sudo apt update && sudo apt install -y nginx certbot python3-certbot-nginx
#
# The script will:
#   1. Validate that .env exists.
#   2. Pull the latest code (if inside a git repo).
#   3. Build and start the containers with docker compose.
#   4. Wait for the database to be healthy then print migration instructions.
#   5. Optionally install the nginx site config and obtain a TLS certificate.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# ── 1. Check required files ────────────────────────────────────────────────────
if [[ ! -f ".env" ]]; then
    echo "ERROR: .env file not found in $(pwd)."
    echo "Copy .env.example to .env and fill in all CHANGE_ME values."
    exit 1
fi

# Load .env into the current shell so variables are available for pg_isready check
set -o allexport
# shellcheck source=.env
source .env
set +o allexport

# Warn if any CHANGE_ME placeholder is still present
if grep -q "CHANGE_ME" .env; then
    echo "WARNING: .env contains un-replaced CHANGE_ME placeholders. Review before continuing."
    read -rp "Continue anyway? [y/N] " _confirm
    [[ "$_confirm" =~ ^[Yy]$ ]] || exit 1
fi

# ── 2. Pull latest code (optional — skip if not in a git repo) ─────────────────
if git rev-parse --git-dir > /dev/null 2>&1; then
    echo "Pulling latest code..."
    git pull --ff-only
fi

# ── 3. Build and start containers ─────────────────────────────────────────────
echo "Building and starting containers..."
docker compose pull db  2>/dev/null || true
docker compose up --build -d

# ── 4. Wait for the database to be healthy ────────────────────────────────────
echo "Waiting for database to be ready..."
_retries=30
until docker compose exec -T db pg_isready -U "${POSTGRES_USER:-postgres}" > /dev/null 2>&1; do
    _retries=$((_retries - 1))
    if [[ $_retries -le 0 ]]; then
        echo "ERROR: Database did not become ready in time."
        docker compose logs db
        exit 1
    fi
    sleep 2
done
echo "Database is ready."

# ── 5. EF Core migrations note ────────────────────────────────────────────────
# The runtime Docker image does not include the .NET SDK or EF Core CLI tools.
# Run migrations from a machine that has the .NET SDK installed:
#
#   dotnet ef database update \
#     --project src/Easshas.Infrastructure \
#     --startup-project src/Easshas.WebApi \
#     --connection "<your-connection-string>"
#
# Or, run migrations once from a temporary SDK container:
#   docker run --rm --network host \
#     -v "$(pwd):/app" -w /app \
#     -e ConnectionStrings__Postgres="${CONNECTIONSTRINGS_POSTGRES}" \
#     mcr.microsoft.com/dotnet/sdk:8.0 \
#     dotnet ef database update \
#       --project src/Easshas.Infrastructure \
#       --startup-project src/Easshas.WebApi
echo ""
echo "NOTE: EF Core migrations are NOT run automatically."
echo "Run them manually from a machine with the .NET SDK — see deploy.sh for instructions."

# ── 6. Nginx setup (interactive, skipped if --no-nginx flag passed) ────────────
if [[ "${1:-}" != "--no-nginx" ]]; then
    NGINX_CONF="/etc/nginx/sites-available/easshas"
    NGINX_ENABLED="/etc/nginx/sites-enabled/easshas"

    if command -v nginx > /dev/null 2>&1; then
        echo ""
        read -rp "Install/update nginx reverse proxy config? [y/N] " _install_nginx
        if [[ "$_install_nginx" =~ ^[Yy]$ ]]; then
            echo "Copying nginx config..."
            sudo cp nginx/easshas.conf "$NGINX_CONF"

            read -rp "Enter your domain name (e.g. eeshasgloss.com): " _domain
            sudo sed -i "s/your-domain\.com/$_domain/g" "$NGINX_CONF"

            [[ -L "$NGINX_ENABLED" ]] || sudo ln -s "$NGINX_CONF" "$NGINX_ENABLED"

            sudo nginx -t && sudo systemctl reload nginx

            read -rp "Obtain a Let's Encrypt TLS certificate with Certbot? [y/N] " _certbot
            if [[ "$_certbot" =~ ^[Yy]$ ]]; then
                sudo certbot --nginx -d "$_domain"
            fi
        fi
    else
        echo "Nginx not found. Skipping nginx setup."
        echo "To set up nginx manually, see: nginx/easshas.conf"
    fi
fi

# ── 7. Print status ────────────────────────────────────────────────────────────
echo ""
echo "Deployment complete."
docker compose ps
echo ""
echo "API is accessible at http://localhost:8080"
echo "Check logs with: docker compose logs -f api"
