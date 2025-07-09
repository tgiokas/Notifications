using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Notifications.Application.DTOs;
using Notifications.Infrastructure.Messaging;

namespace Notifications.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KafkaProducerController : ControllerBase
{
    private readonly KafkaProducerService<string, string> _kafkaProducer;
    private readonly ILogger<KafkaProducerController> _logger;

    public KafkaProducerController(KafkaProducerService<string, string> kafkaProducer, ILogger<KafkaProducerController> logger)
    {
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    [HttpPost("SendMessage")]
    public async Task<IActionResult> SendMessage(KafkaMessage<string> message)
    {
        try
        {
            var result = await _kafkaProducer.ProduceMessageAsync("testKey", message);
            return Ok(new
            {
                MessageId = message.Id,
                TopicPartitionOffset = result.TopicPartitionOffset.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            return StatusCode(500, "Failed to send message to Kafka");
        }
    }
}

