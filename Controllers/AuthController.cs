using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FirebaseAdmin.Auth;

namespace CryptostellerAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        [HttpPost("custom-token")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCustomToken([FromBody] CustomTokenRequest request)
        {
            if (string.IsNullOrEmpty(request.FirebaseUid))
                return BadRequest(new { message = "FirebaseUid is required." });

            var customToken = await FirebaseAuth.DefaultInstance
                .CreateCustomTokenAsync(request.FirebaseUid);

            return Ok(new { customToken });
        }
    }

    public class CustomTokenRequest
    {
        public string FirebaseUid { get; set; } = default!;
    }
}

