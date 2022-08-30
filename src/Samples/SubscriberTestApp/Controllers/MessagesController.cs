using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SubscriberTestApp.Internal.Services;

namespace SubscriberTestApp.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;

    public MessagesController(IMessageService messageService)
    {
        _messageService = messageService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        return Ok(await _messageService.GetAllMessages());
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById([FromRoute] [Required] string id)
    {
        var message = await _messageService.GetMessage(id);
        if (message == null)
            return NotFound();
        return Ok(message);
    }

    [HttpGet(template: "filtered")]
    public async Task<IActionResult> Filter([FromQuery] string idPrefix)
    {
        return Ok(await _messageService.FilterMessages(idPrefix));
    }

    [HttpGet(template: "count")]
    public async Task<IActionResult> Count()
    {
        return Ok(await _messageService.GetCountOfMessages());
    }

    [HttpDelete(template: "all")]
    public async Task<IActionResult> DeleteAll()
    {
        await _messageService.ClearMessages();
        return NoContent();
    }

    [HttpGet(template: "workers")]
    public async Task<IActionResult> GetActiveWorkers()
    {
        return Ok(await _messageService.GetActiveWorkers());
    }
}