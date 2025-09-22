using Microsoft.AspNetCore.Mvc;
using ActoX.Application.DTOs;
using ActoX.Application.Interfaces;

namespace ActoX.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectController(IProjectService projectService) : ControllerBase
    {
        private readonly IProjectService _projectService = projectService;

        [HttpPost("verify")]
        public async Task<IActionResult> VerifyConnection([FromBody] VerifyConnectionRequest request)
        {
            var isValid = await _projectService.VerifyConnectionAsync(request);
            if (isValid)
                return Ok(new { Message = "Connection successful" });
            return BadRequest(new { Message = "Connection failed" });
        }

        [HttpPost("link")]
        public async Task<IActionResult> LinkProject([FromBody] LinkProjectRequest request)
        {
            var userId = 1; // Replace with authenticated UserID
            var response = await _projectService.LinkProjectAsync(request, userId);
            return Ok(response);
        }
    }
}