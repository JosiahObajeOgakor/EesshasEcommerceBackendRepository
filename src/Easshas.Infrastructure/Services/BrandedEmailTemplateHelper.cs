using System;
using Microsoft.Extensions.Configuration;

namespace Easshas.Infrastructure.Services
{
    public static class BrandedEmailTemplateHelper
    {
        public static string GetBrandedHtml(IConfiguration config, string title, string contentHtml)
        {
            var eeshasLogo = config["Email:EeshasLogoUrl"] ?? "https://res.cloudinary.com/detpqzhnq/image/upload/v1771726285/ChatGPT_Image_Feb_22_2026_03_10_41_AM_gnwmw7.png";
            var springutechLogo = config["Email:SpringutechLogoUrl"] ?? "https://res.cloudinary.com/detpqzhnq/image/upload/v1771728249/footer_dqv7vs.png";
            var year = DateTime.Now.Year;

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; background-color: #f4f4f4; }}
        .container {{ max-width: 600px; margin: 20px auto; background: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 10px rgba(0,0,0,0.1); }}
        .header {{ background-color: #ffffff; padding: 20px; text-align: center; border-bottom: 2px solid #f0f0f0; }}
        .header img {{ max-height: 80px; }}
        .content {{ padding: 30px; min-height: 200px; }}
        .footer {{ background-color: #fafafa; padding: 20px; text-align: center; font-size: 12px; color: #777; border-top: 1px solid #eee; }}
        .footer img {{ max-height: 30px; margin-bottom: 10px; opacity: 0.8; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #007bff; color: #ffffff; text-decoration: none; border-radius: 4px; font-weight: bold; margin-top: 20px; }}
        h1, h2 {{ color: #1a1a1a; }}
        .order-table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
        .order-table th, .order-table td {{ text-align: left; padding: 12px; border-bottom: 1px solid #eee; }}
        .order-table th {{ background-color: #f9f9f9; }}
        .total-row {{ font-weight: bold; font-size: 1.1em; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <img src='{eeshasLogo}' alt='Eeshas Logo' />
        </div>
        <div class='content'>
            <h1>{title}</h1>
            {contentHtml}
        </div>
        <div class='footer'>
            <img src='{springutechLogo}' alt='Springutechafrica Logo' /><br/>
            &copy; {year} Eeshas. All rights reserved.<br/>
            Developed and Maintained by <strong>Springutechafrica Limited</strong>
        </div>
    </div>
</body>
</html>";
        }
    }
}
