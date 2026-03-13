using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Easshas.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("api/agentic")]
    [AllowAnonymous]
    public class AgenticChatController : ControllerBase
    {
        private readonly IConversationService _conversation;
        private readonly Easshas.Application.Abstractions.IAgenticService _agentic;
        private readonly Easshas.Application.Abstractions.IIntentClassifier _intentClassifier;
        private readonly ILogger<AgenticChatController> _logger;

        public AgenticChatController(IConversationService conversation, Easshas.Application.Abstractions.IAgenticService agentic, Easshas.Application.Abstractions.IIntentClassifier intentClassifier, ILogger<AgenticChatController> logger)
        {
            _conversation = conversation;
            _agentic = agentic;
            _intentClassifier = intentClassifier;
            _logger = logger;
        }

        public class ChatRequest
        {
            public string Message { get; set; } = string.Empty;
            public string? UserId { get; set; }
            // Optional explicit mode: "agentic" or "chat". If omitted, classifier decides.
            public string? Mode { get; set; }
            // If true, confirms agentic actions when classifier asks for confirmation
            public bool? Confirm { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Handle([FromBody] ChatRequest req)
        {
            // Priority: header X-Agent-Mode -> body.Mode -> classifier
            var headerMode = Request.Headers["X-Agent-Mode"].FirstOrDefault();
            var mode = (headerMode ?? req.Mode ?? string.Empty).Trim().ToLowerInvariant();

            // Read confirmation from header or body
            var headerConfirm = Request.Headers["X-Agent-Confirm"].FirstOrDefault();
            var confirmFlag = false;
            if (!string.IsNullOrEmpty(headerConfirm) && bool.TryParse(headerConfirm, out var parsed)) confirmFlag = parsed;
            if (!confirmFlag && req.Confirm.HasValue) confirmFlag = req.Confirm.Value;

            if (string.IsNullOrEmpty(mode))
            {
                // Use intent classifier to pick mode when not explicitly provided
                var (predicted, confidence) = await _intentClassifier.ClassifyAsync(req.Message ?? string.Empty);
                const double threshold = 0.80;
                _logger.LogInformation("IntentClassifier predicted={Predicted} confidence={Confidence} for message='{Msg}'", predicted, confidence, req.Message);
                if (confidence >= threshold)
                {
                    mode = predicted;
                }
                else
                {
                    // low confidence: if predicted agentic, require explicit confirmation
                    if (predicted == "agentic")
                    {
                        if (confirmFlag)
                        {
                            _logger.LogInformation("Confirmation flag present; proceeding with agentic despite low confidence={Confidence}", confidence);
                            mode = "agentic";
                        }
                        else
                        {
                            _logger.LogInformation("Low-confidence agentic detected; returning needConfirmation (predicted={Predicted}, confidence={Confidence})", predicted, confidence);
                            return StatusCode(StatusCodes.Status202Accepted, new { needConfirmation = true, predicted = predicted, confidence });
                        }
                    }
                    else
                    {
                        mode = "chat";
                    }
                }
            }

            if (mode == "agentic" || mode == "llm")
            {
                var userId = req.UserId ?? "guest";
                var result = await _agentic.HandleMessageAsync(userId, req.Message ?? string.Empty);
                return Ok(result);
            }

            // default to simple chat
            var response = await _conversation.HandleMessageAsync(req.UserId ?? "guest", req.Message ?? string.Empty);
            return Ok(new { response });
        }
    }
}
