using Microsoft.AspNetCore.Mvc;
using MyApi.Models;
using MyApi.Services;

namespace MyApi.Controllers;

[ApiController]
// [Authorize]
[Route("api/notifications")]
public class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<Notification>>> GetAll()
    {
        return Ok(await notificationService.GetAllAsync());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Notification>> GetById(Guid id)
    {
        var notification = await notificationService.GetByIdAsync(id);
        return notification is null ? NotFound() : Ok(notification);
    }

    [HttpPost]
    public async Task<ActionResult<Notification>> Create(CreateNotificationRequest request)
    {
        if (!notificationService.IsSupportedType(request.Type))
        {
            return BadRequest(new
            {
                error = $"Type must be one of: {string.Join(", ", notificationService.SupportedTypes)}"
            });
        }

        var notification = await notificationService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = notification.Id }, notification);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Notification>> Update(Guid id, UpdateNotificationRequest request)
    {
        if (!notificationService.IsSupportedType(request.Type))
        {
            return BadRequest(new
            {
                error = $"Type must be one of: {string.Join(", ", notificationService.SupportedTypes)}"
            });
        }

        var notification = await notificationService.UpdateAsync(id, request);
        return notification is null ? NotFound() : Ok(notification);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        return await notificationService.DeleteAsync(id) ? NoContent() : NotFound();
    }
}
