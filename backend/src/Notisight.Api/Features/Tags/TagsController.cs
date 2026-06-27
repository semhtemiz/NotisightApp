using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notisight.Api.Domain.Entities;
using Notisight.Api.Features.Tags.Contracts;
using Notisight.Api.Infrastructure.Auth;
using Notisight.Api.Infrastructure.Persistence;

namespace Notisight.Api.Features.Tags;

[ApiController]
[Route("tags")]
[Authorize]
public sealed class TagsController(
    ApplicationDbContext dbContext,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TagResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TagResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();

        var tags = await dbContext.Tags
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Name)
            .Select(x => new TagResponse(x.Id, x.Name, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(tags);
    }

    [HttpPost]
    [ProducesResponseType(typeof(TagResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<TagResponse>> Create(
        [FromBody] TagRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim()
        };

        dbContext.Tags.Add(tag);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/tags/{tag.Id}", new TagResponse(tag.Id, tag.Name, tag.CreatedAtUtc));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TagResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TagResponse>> Update(
        Guid id,
        [FromBody] TagRequest request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();
        var tag = await dbContext.Tags.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userId,
            cancellationToken);

        if (tag is null)
        {
            throw new KeyNotFoundException("Tag was not found.");
        }

        tag.Name = request.Name.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new TagResponse(tag.Id, tag.Name, tag.CreatedAtUtc));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();
        var tag = await dbContext.Tags.SingleOrDefaultAsync(
            x => x.Id == id && x.UserId == userId,
            cancellationToken);

        if (tag is null)
        {
            return NoContent();
        }

        var noteTags = await dbContext.NoteTags
            .Where(x => x.TagId == id)
            .ToListAsync(cancellationToken);

        if (noteTags.Count > 0)
        {
            dbContext.NoteTags.RemoveRange(noteTags);
        }

        dbContext.Tags.Remove(tag);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
