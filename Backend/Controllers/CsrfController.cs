using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace ActoEngine.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CsrfController(IAntiforgery antiforgery) : ControllerBase
    {
        private readonly IAntiforgery _antiforgery = antiforgery;

        [HttpGet("token")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public IActionResult GetToken()
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            
            // The cookie is set by GetAndStoreTokens based on startup config
            // But we can also explicitly set it if needed, or just return it
            // The startup config sets Cookie.Name = "XSRF-TOKEN" and HttpOnly = false
            // So the browser should receive it.
            
            return Ok(new { token = tokens.RequestToken });
        }
    }
}
