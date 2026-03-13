using System;
using System.IO;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;

namespace ZohoTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try 
            {
                string configPath = "appsettings.json";
                if (!File.Exists(configPath)) {
                    configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                }
                if (!File.Exists(configPath)) {
                    configPath = "../appsettings.json";
                }

                if (!File.Exists(configPath)) {
                    Console.WriteLine("RESULT:GENERAL_ERROR:appsettings.json not found in any expected location.");
                    return;
                }

                var config = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(configPath)).Build();
                var host = config["Email:Smtp:Host"];
                var port = int.Parse(config["Email:Smtp:Port"] ?? "587");
                var username = config["Email:Smtp:Username"];
                var password = config["Email:Smtp:Password"];
                var from = config["Email:From"] ?? "support@Springuptechafrica.com";

                using var client = new SmtpClient();
                client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                var socketOption = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
                await client.ConnectAsync(host, port, socketOption);
                try
                {
                    await client.AuthenticateAsync(username, password);
                    Console.WriteLine("RESULT:AUTH_SUCCESS");

                                        var message = new MimeMessage();
                                        message.From.Add(MailboxAddress.Parse(from));
                                        message.To.Add(MailboxAddress.Parse("josiahobaje.dev@gmail.com"));
                                        message.Subject = "Payment Receipt — Eeshas Gloss";

                                        var headerLogo = config["Email:EeshasLogoUrl"] ?? "https://res.cloudinary.com/detpqzhnq/image/upload/v1771726285/ChatGPT_Image_Feb_22_2026_03_10_41_AM_gnwmw7.png";
                                        var footerLogo = config["Email:SpringutechLogoUrl"] ?? "https://res.cloudinary.com/detpqzhnq/image/upload/v1771719389/WSij001_wavi42.svg";

                                        var html = $@"
<!doctype html>
<html>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width,initial-scale=1' />
    <style>
        :root {{ color-scheme: light dark; }}
        body {{ margin:0; padding:0; background:#eef2f5; font-family:Inter, -apple-system, 'Segoe UI', Roboto, Arial; -webkit-font-smoothing:antialiased; }}
        .wrap {{ padding:14px 12px 6px 12px; display:flex; justify-content:center; }}
        .panel {{ width:100%; max-width:720px; background:#ffffff; border-radius:12px; overflow:hidden; box-shadow:0 8px 20px rgba(16,24,40,0.06); }}
        .panel-header {{ padding:18px 18px 12px 18px; text-align:center; background:linear-gradient(0deg, #ffffff, #ffffff); }}
        .panel-header img {{ max-height:64px; }}
        .panel-body {{ padding:18px 20px 12px 20px; color:#111827; }}
        h1 {{ margin:0 0 6px 0; font-size:18px; color:#0f172a; }}
        p.lead {{ margin:0 0 12px 0; color:#374151; }}
        .receipt {{ background:#f8fafc; border:1px solid #eef2f6; padding:12px; border-radius:8px; display:flex; justify-content:space-between; gap:12px; align-items:center; }}
        .receipt .left {{ flex:1; }}
        .receipt .right {{ text-align:right; min-width:120px; }}
        .label {{ display:block; font-size:12px; color:#6b7280; }}
        .value {{ font-weight:700; font-size:14px; color:#0b1220; }}
        .items {{ margin-top:12px; border-top:1px solid #f1f5f9; padding-top:12px; }}
        .item {{ display:flex; justify-content:space-between; padding:8px 0; border-bottom:1px dashed #eef2f6; color:#374151; }}
        .item:last-child {{ border-bottom:0; }}
        .total {{ display:flex; justify-content:space-between; padding:10px 0 0 0; font-weight:700; font-size:15px; color:#0b1220; }}
        .cta {{ margin-top:12px; }}
        .btn {{ display:inline-block; padding:9px 14px; background:#0ea5a3; color:#fff; border-radius:8px; text-decoration:none; font-weight:600; }}
        .panel-footer {{ padding:10px 12px; text-align:center; background:#fbfdfe; border-top:1px solid #eef6f9; }}
        .panel-footer img {{ max-height:36px; opacity:0.95; display:block; margin:0 auto; }}
        @media (max-width:520px) {{ .receipt {{ flex-direction:column; text-align:left; }} .receipt .right {{ text-align:left; min-width:auto; }} }}
    </style>
</head>
<body>
    <div class='wrap'>
        <div class='panel'>
            <div class='panel-header'>
                <img src='{headerLogo}' alt='Eeshas' />
            </div>
            <div class='panel-body'>
                <h1>Payment Receipt</h1>
                <p class='lead'>Thank you — we have received your payment. A summary is provided below for your records.</p>

                <div class='receipt' style='display:grid; grid-template-columns:repeat(3,1fr); gap:12px; align-items:center;'>
                    <div style='text-align:left;'>
                        <span class='label'>Transaction Reference</span>
                        <div class='value'>RECEIPT-2026-0001</div>
                    </div>
                    <div style='text-align:center;'>
                        <span class='label'>Amount Paid</span>
                        <div class='value'>₦10,000.00</div>
                    </div>
                    <div style='text-align:right;'>
                        <span class='label'>Payment Date</span>
                        <div class='value'>{DateTime.Now:yyyy-MM-dd}</div>
                        <div style='font-size:12px;color:#6b7280;margin-top:4px'>{DateTime.Now:HH:mm}</div>
                    </div>
                </div>

                <div class='items'>
                    <div class='item'><div>Sample Product</div><div>₦10,000.00</div></div>
                    <div class='total'><div>Total</div><div>₦10,000.00</div></div>
                </div>

                <div class='cta'>
                    <a class='btn' href='https://eeshasgloss.com'>View Order</a>
                </div>
            </div>
            <div class='panel-footer'>
                <div style='max-width:560px;margin:0 auto;padding:0 6px;'>
                    <img src='https://res.cloudinary.com/detpqzhnq/image/upload/v1771728249/footer_dqv7vs.png' alt='Springuptechafrica' style='display:block;margin:0 auto;max-width:220px;height:auto' />
                    <div style='font-size:12px;color:#6b7280;margin-top:6px;line-height:1.2;'>&copy; {DateTime.Now.Year} Eeshas — Developed by Springuptechafrica Limited</div>
                </div>
            </div>
        </div>
    </div>
</body>
</html>
";

                                        var bodyBuilder = new BodyBuilder { HtmlBody = html };
                                        message.Body = bodyBuilder.ToMessageBody();

                                        await client.SendAsync(message);
                                        Console.WriteLine("RESULT:EMAIL_SENT");

                                        // Send admin notification with inventory breakdown
                                        var adminMessage = new MimeMessage();
                                        adminMessage.From.Add(MailboxAddress.Parse(from));
                                        adminMessage.To.Add(MailboxAddress.Parse("josiahobaje.dev@gmail.com"));
                                        adminMessage.Subject = "Admin: New Purchase Received — Inventory Update";

                                        var adminHtml = $@"<!doctype html>
<html>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width,initial-scale=1' />
    <style>
        body {{ margin:0; padding:18px; background:#f1f5f9; font-family:Inter, -apple-system, 'Segoe UI', Roboto, Arial; color:#0f172a; }}
        .container {{ max-width:780px; margin:0 auto; background:#ffffff; border-radius:10px; overflow:hidden; box-shadow:0 8px 20px rgba(2,6,23,0.08); }}
        .top {{ padding:18px 22px; text-align:center; }}
        .top img {{ max-height:64px; }}
        .body {{ padding:20px 24px; }}
        h2 {{ margin:0 0 8px 0; font-size:18px; }}
        p.lead {{ margin:0 0 14px 0; color:#374151; }}
        .order-meta {{ display:flex; gap:12px; flex-wrap:wrap; margin:12px 0 16px 0; }}
        .meta-box {{ background:#f8fafc; padding:10px 12px; border-radius:8px; border:1px solid #eef2f6; min-width:140px; }}
        .meta-box strong {{ display:block; font-size:13px; color:#111827; }}
        table {{ width:100%; border-collapse:collapse; margin-top:12px; }}
        th, td {{ text-align:left; padding:10px; border-bottom:1px solid #eef2f6; font-size:14px; color:#374151; }}
        th {{ background:#fbfdfe; font-weight:700; color:#0b1220; }}
        .badge {{ display:inline-block; padding:6px 10px; background:#fde68a; border-radius:999px; font-weight:700; font-size:13px; color:#92400e; }}
        .footer {{ padding:14px 18px; text-align:center; background:#fbfdfe; border-top:1px solid #eef6f9; }}
        .footer img {{ max-height:36px; display:block; margin:0 auto 8px auto; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='top'><img src='{headerLogo}' alt='Eeshas' /></div>
        <div class='body'>
            <h2>New Purchase — Admin Notification</h2>
            <p class='lead'>A payment was received and an order has been processed. Below is a summary and current inventory breakdown for the items purchased.</p>

            <div class='order-meta'>
                <div class='meta-box'><strong>Order ID</strong><span>ORD-2026-0001</span></div>
                <div class='meta-box'><strong>Tracking</strong><span>TRK-9001</span></div>
                <div class='meta-box'><strong>Paid</strong><span>₦10,000.00</span></div>
                <div class='meta-box'><strong>Customer</strong><span>Josiah Obaje</span></div>
            </div>

            <h3 style='margin-top:6px'>Items Purchased</h3>
            <table>
                <thead>
                    <tr><th>Item</th><th>Qty Ordered</th><th>Remaining Inventory</th></tr>
                </thead>
                <tbody>
                    <tr><td>Sample Product</td><td>1</td><td>24</td></tr>
                    <tr><td>Another Item</td><td>2</td><td>5</td></tr>
                    <tr><td>Complementary Accessory</td><td>1</td><td>0</td></tr>
                </tbody>
            </table>

            <p style='margin-top:14px'>Inventory summary above highlights current stock after this purchase. Items with low or zero inventory require restocking.</p>
            <p><span class='badge'>Action: Review low-stock items</span></p>
        </div>
        <div class='footer'>
            <img src='https://res.cloudinary.com/detpqzhnq/image/upload/v1771728249/footer_dqv7vs.png' alt='Springuptechafrica' />
            <div style='color:#6b7280;font-size:13px;'>&copy; {DateTime.Now.Year} Eeshas — Springuptechafrica Limited</div>
        </div>
    </div>
</body>
</html>";

                                        var adminBody = new BodyBuilder { HtmlBody = adminHtml };
                                        adminMessage.Body = adminBody.ToMessageBody();
                                        await client.SendAsync(adminMessage);
                                        Console.WriteLine("RESULT:ADMIN_EMAIL_SENT");
                }
                catch (Exception authEx)
                {
                    Console.WriteLine("RESULT:AUTH_FAILED:" + authEx.Message.Replace("\n", " ").Replace("\r", " "));
                }
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine("RESULT:GENERAL_ERROR:" + ex.Message);
            }
        }
    }
}
