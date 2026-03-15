#!/usr/bin/env bash
# deploy-ubuntu.sh – Deploy the Easshas API on a fresh Ubuntu 22.04 / 24.04 server
#
# Usage (run as root or with sudo):
#   sudo bash deploy-ubuntu.sh
#
# What this script does:
#   1. Installs .NET 8 runtime, PostgreSQL client tools, Nginx, and Certbot
#   2. Creates a dedicated 'easshas' system user
#   3. Creates the directory layout under /opt/easshas
#   4. Prompts for the .env values and writes them to /etc/easshas/environment
#   5. Copies the systemd unit file and enables the service
#   6. Copies the Nginx site config and reloads Nginx
#
# After running this script you still need to:
#   a. Build/publish the app and copy the output to /opt/easshas/api/
#      (or let the GitHub Actions workflow do it automatically – see .github/workflows/deploy.yml)
#   b. Obtain a TLS certificate:
#      sudo certbot --nginx -d api.eeshasgloss.com
#   c. Start the service:
#      sudo systemctl start easshas-api

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
APP_DIR=/opt/easshas/api
ENV_FILE=/etc/easshas/environment
SERVICE_NAME=easshas-api

# ── 1. Install dependencies ──────────────────────────────────────────────────
echo "==> Installing .NET 8 runtime, Nginx, and Certbot …"
apt-get update -qq
apt-get install -y --no-install-recommends \
    wget ca-certificates gnupg lsb-release apt-transport-https \
    nginx certbot python3-certbot-nginx

# .NET 8 runtime from Microsoft feed
if ! dpkg -l dotnet-runtime-8.0 &>/dev/null; then
    wget -q https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb \
        -O /tmp/packages-microsoft-prod.deb
    dpkg -i /tmp/packages-microsoft-prod.deb
    apt-get update -qq
    apt-get install -y --no-install-recommends aspnetcore-runtime-8.0
fi

# ── 2. Create system user ────────────────────────────────────────────────────
echo "==> Creating system user 'easshas' …"
if ! id -u easshas &>/dev/null; then
    useradd --system --no-create-home --shell /usr/sbin/nologin easshas
fi

# ── 3. Create directory layout ───────────────────────────────────────────────
echo "==> Creating $APP_DIR …"
mkdir -p "$APP_DIR"
chown -R easshas:easshas /opt/easshas

# ── 4. Write environment file ────────────────────────────────────────────────
echo "==> Writing environment file to $ENV_FILE …"
mkdir -p /etc/easshas
chmod 700 /etc/easshas

if [[ ! -f "$ENV_FILE" ]]; then
    # Copy from the template and prompt the operator to fill in secrets
    cp "$REPO_ROOT/.env.example" "$ENV_FILE"
    chmod 600 "$ENV_FILE"
    echo ""
    echo "  *** ACTION REQUIRED ***"
    echo "  Open $ENV_FILE and replace every CHANGE_ME_* placeholder with real values."
    echo "  Then re-run: sudo systemctl restart $SERVICE_NAME"
    echo ""
else
    echo "  $ENV_FILE already exists – skipping (edit manually to update secrets)."
fi

# ── 5. Install and enable systemd service ────────────────────────────────────
echo "==> Installing systemd service …"
cp "$SCRIPT_DIR/easshas-api.service" /etc/systemd/system/${SERVICE_NAME}.service
systemctl daemon-reload
systemctl enable "$SERVICE_NAME"
echo "  Service enabled. Start it after copying the published app to $APP_DIR:"
echo "    sudo systemctl start $SERVICE_NAME"

# ── 6. Configure Nginx ───────────────────────────────────────────────────────
echo "==> Configuring Nginx …"
cp "$REPO_ROOT/nginx/easshas.conf" /etc/nginx/sites-available/easshas
ln -sf /etc/nginx/sites-available/easshas /etc/nginx/sites-enabled/easshas
# Remove the default site if it is still there
rm -f /etc/nginx/sites-enabled/default
nginx -t
systemctl reload nginx

echo ""
echo "══════════════════════════════════════════════════════"
echo " Setup complete!  Next steps:"
echo ""
echo "  1. Edit $ENV_FILE with real credentials."
echo ""
echo "  2. Publish and copy the app:"
echo "     dotnet publish src/Easshas.WebApi -c Release -o /tmp/easshas-publish"
echo "     sudo cp -r /tmp/easshas-publish/* $APP_DIR/"
echo "     sudo chown -R easshas:easshas $APP_DIR"
echo ""
echo "  3. Apply EF Core migrations (run once from the repo):"
echo "     dotnet ef database update \\"
echo "       --project src/Easshas.Infrastructure \\"
echo "       --startup-project src/Easshas.WebApi \\"
echo "       --context Easshas.Infrastructure.Persistence.AppDbContext"
echo ""
echo "  4. Obtain a TLS certificate:"
echo "     sudo certbot --nginx -d api.eeshasgloss.com"
echo ""
echo "  5. Start the service:"
echo "     sudo systemctl start $SERVICE_NAME"
echo "     sudo systemctl status $SERVICE_NAME"
echo "══════════════════════════════════════════════════════"
