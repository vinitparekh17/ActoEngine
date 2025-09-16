using Microsoft.AspNetCore.Mvc;

namespace ActoX.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectController : ControllerBase
{
    [HttpGet("create-project")]
    public JsonResult CreateProject()
    {
        return new JsonResult(new { Message = "Project created successfully" });
    }
}
