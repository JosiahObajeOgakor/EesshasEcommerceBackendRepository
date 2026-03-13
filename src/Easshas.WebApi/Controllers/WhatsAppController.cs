using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("webhook/whatsapp")]
    public class WhatsAppController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IConversationService _conversation;
        private readonly IWhatsAppNotifier _whatsApp;

        public WhatsAppController(IConfiguration config, IConversationService conversation, IWhatsAppNotifier whatsApp)
        {
            _config = config;
            _conversation = conversation;
            _whatsApp = whatsApp;
        }

        // Verification for Meta Webhooks
        [HttpGet]
        public IActionResult Verify()
        {
            var mode = Request.Query["hub.mode"].ToString();
            var token = Request.Query["hub.verify_token"].ToString();
            var challenge = Request.Query["hub.challenge"].ToString();
            var expected = _config["WhatsApp:VerifyToken"];

            if (mode == "subscribe" && (!string.IsNullOrEmpty(expected) ? token == expected : true))
            {
                return Content(challenge);
            }
            return Forbid();
        }

        [HttpPost]
        public async Task<IActionResult> Receive()
        {
            using var doc = await JsonDocument.ParseAsync(Request.Body);
            var root = doc.RootElement;
            var messages = root.TryGetProperty("entry", out var entry) && entry.GetArrayLength() > 0
                ? entry[0].TryGetProperty("changes", out var changes) && changes.GetArrayLength() > 0
                    ? changes[0].TryGetProperty("value", out var value) && value.TryGetProperty("messages", out var msgs) ? msgs : default
                    : default
                : default;

            if (messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() > 0)
            {
                var msg = messages[0];
                var text = msg.TryGetProperty("text", out var txt) && txt.TryGetProperty("body", out var body) ? body.GetString() : null;
                var fromPhone = msg.TryGetProperty("from", out var from) ? from.GetString() : null;
                if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(fromPhone))
                {
                    var reply = await _conversation.HandleMessageAsync(fromPhone!, text!);
                    await _whatsApp.SendTextAsync(fromPhone!, reply);
                }
            }

            return Ok();
        }
    }
}
