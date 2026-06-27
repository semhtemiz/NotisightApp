using Microsoft.AspNetCore.Mvc;
using Notisight.Api.Contracts;

namespace Notisight.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get()
    {
        return Ok(new HealthResponse(
            "ok",
            "Notisight API is running.",
            DateTimeOffset.UtcNow));
    }
}
