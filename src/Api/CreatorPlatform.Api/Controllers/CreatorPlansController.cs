using CreatorPlatform.Creators.Application.Dtos;
using CreatorPlatform.Creators.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CreatorPlatform.Api.Controllers;

[ApiController]
[Route("api/creator-plans")]
public sealed class CreatorPlansController : ControllerBase
{
    private readonly ICreatorPlanService _creatorPlanService;

    public CreatorPlansController(ICreatorPlanService creatorPlanService)
    {
        _creatorPlanService = creatorPlanService;
    }

    [HttpGet]
    public async Task<ActionResult<List<CreatorPlanResponseDto>>> ListActive(CancellationToken ct)
    {
        var response = await _creatorPlanService.ListActiveAsync(ct);

        return Ok(response);
    }
}
