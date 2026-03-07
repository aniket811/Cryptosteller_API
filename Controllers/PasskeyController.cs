using Azure.Core;
using CryptostellerAPI.Models;
using CryptostellerAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace CryptostellerAPI.Controllers
{
    [Route("api/[controller]")]
    [Authorize]
    [ApiController]
    public class PasskeyController : ControllerBase
    {
        private readonly PasskeyService passkeyService;
        public PasskeyController(PasskeyService _passkeyService)
        {
                this.passkeyService= _passkeyService;
        }
        [HttpPost("register/options")]
        public async Task<IActionResult> RegisterOptions()
        {
            var userId = User.FindFirstValue("user_id")!;   // Firebase UID
            var userEmail = User.FindFirstValue("email")!;
            var displayName = User.FindFirstValue("name") ?? userEmail; 

            var options = await passkeyService.GenerateRegistrationOptionsAsync(userId, userEmail, displayName );
            return Ok(options);

        }
        // ──────────────────────────────────────────────────────────
        //  REGISTRATION — Step 3
        //  Angular sends the credential the device created.
        //  We verify it and save to SQL Server.
        // ──────────────────────────────────────────────────────────
        [HttpPost("register/verify")]
        public async Task<IActionResult> VerifyPasskey([FromBody] RegisterVerifyRequest registerVerifyRequest)
        {
            var (uid, email, displayName) = GetFirebaseClaims();

            var response = await passkeyService.VerifyRegistrationAsync(email,uid, displayName,registerVerifyRequest);

            return response.Success ? Ok(response) : BadRequest(response);
        }

        [HttpPost("authenticate/options")]
        [AllowAnonymous]
        public async Task<IActionResult> AuthenticateOptions([FromBody] AuthOptionsRequest request)
        {
            var options = await passkeyService.GenerateAuthOptionsAsync(request);
            return Ok(options);
        }

        [HttpPost("authenticate/verify")]
        [AllowAnonymous]
        public async Task<IActionResult> AuthenticateVerify([FromBody] AuthVerifyRequest request)
        {
            var response = await passkeyService.VerifyAuthenticationAsync(request);
            return response.Success ? Ok(response) : BadRequest(response);
        }

        //Helper 
        private (string uid, string email, string displayName) GetFirebaseClaims()
        {
            var uid = User.FindFirstValue("user_id")
                              ?? throw new UnauthorizedAccessException("Firebase UID missing from token.");
            var email = User.FindFirstValue("email") ?? string.Empty;
            var displayName = User.FindFirstValue("name") ?? email;

            return (uid, email, displayName);
        }

    }
}
