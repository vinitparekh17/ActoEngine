using Microsoft.AspNetCore.Mvc;

namespace ActoX.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    [HttpGet("get-user")]
    public JsonResult GetUser()
    {
        return new JsonResult(new { Message = "User fetched successfully" });
    }
}