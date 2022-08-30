using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SenderTestApp.Internal;
using SenderTestApp.Models;

namespace SenderTestApp.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MessagesController : ControllerBase
{
    private readonly IMessagesService _messagesService;

    public MessagesController(IMessagesService messagesService)
    {
        _messagesService = messagesService;
    }

    [HttpPost(Name = "PublishMessage")]
    public async Task<IActionResult> PublishMessage([FromBody] SendMessageRequest sendMessageRequest)
    {
        var messageId = await _messagesService.PublishMessage(sendMessageRequest);
        return CreatedAtRoute("PublishMessage", messageId);
    }

    [HttpPost(template: "batch", Name = "PublishMessages")]
    public async Task<IActionResult> PublishMessages([FromBody] IEnumerable<SendMessageRequest> sendMessageRequests)
    {
        var messageIds = await _messagesService.PublishMessages(sendMessageRequests);
        return CreatedAtRoute("PublishMessages", messageIds);
    }

    [HttpGet(template: "poll/{queueName}")]
    public async Task<IActionResult> Poll([FromRoute] [Required] string queueName)
    {
        var messages = await _messagesService.PollMessages(queueName);
        return Ok(messages);
    }
}