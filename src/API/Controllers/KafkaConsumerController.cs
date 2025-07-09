
using Microsoft.AspNetCore.Mvc;
using Notifications.Infrastructure.Messaging;

namespace DMSApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class KafkaConsumerController : ControllerBase
    {
        private readonly MessageStoreService<string> _messageStore;

        public KafkaConsumerController(MessageStoreService<string> messageStore)
        {
            _messageStore = messageStore;
        }

        [HttpGet("GetMessages")]
        public IActionResult GetMessages()
        {
            var messages = _messageStore.GetMessages();
            return Ok(messages);
        }
    }
}
