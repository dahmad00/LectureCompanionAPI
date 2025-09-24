using LectureCompanion.Api.Models; // 👈 for GoogleAuthSettings
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Google.Apis.Auth;   // 👈 Install Google.Apis.Auth NuGet package
using System.Text.Json;

namespace LectureCompanion.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly GoogleAuthSettings _googleSettings;

        public AuthController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IOptions<GoogleAuthSettings> googleSettings)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _googleSettings = googleSettings.Value;
        }

        [HttpPost("signup")]
        public async Task<IActionResult> SignUp([FromBody] SignUpRequest request)
        {
            var user = new AppUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (result.Succeeded)
                return Ok("User created");

            return BadRequest(result.Errors);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var result = await _signInManager.PasswordSignInAsync(
                request.Email,
                request.Password,
                isPersistent: false,
                lockoutOnFailure: false);

            if (result.Succeeded)
                return Ok("Login successful");

            return Unauthorized();
        }

        // 🔹 Endpoint for Google sign-in
        [HttpPost("google-signin")]
        public async Task<IActionResult> GoogleSignIn([FromBody] ExternalLoginRequest request)
        {
            using var client = new HttpClient();

            var values = new Dictionary<string, string>
            {
                { "code", request.Code },
                { "client_id", _googleSettings.ClientId },
                { "client_secret", _googleSettings.ClientSecret },
                { "redirect_uri", "https://<your-backend-domain>/api/auth/oauth2redirect" }, // must match Google Console
                { "grant_type", "authorization_code" }
            };

            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync("https://oauth2.googleapis.com/token", content);

            if (!response.IsSuccessStatusCode)
                return Unauthorized("Failed to exchange code");

            var json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(json);

            // ✅ Validate the ID token
            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(tokenResponse.IdToken);
            }
            catch (Exception)
            {
                return Unauthorized("Invalid Google token");
            }

            // ✅ Check if user exists
            var user = await _userManager.FindByEmailAsync(payload.Email);
            if (user == null)
            {
                user = new AppUser
                {
                    UserName = payload.Email,
                    Email = payload.Email,
                    FullName = payload.Name
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                    return BadRequest(createResult.Errors);

                var info = new UserLoginInfo("GOOGLE", payload.Subject, "GOOGLE");
                await _userManager.AddLoginAsync(user, info);
            }

            await _signInManager.SignInAsync(user, isPersistent: false);

            return Ok("Google login successful");
        }

        // DTO for token response
        public class GoogleTokenResponse
        {
            public string AccessToken { get; set; }
            public string ExpiresIn { get; set; }
            public string RefreshToken { get; set; }
            public string Scope { get; set; }
            public string IdToken { get; set; }
            public string TokenType { get; set; }
        }
    }

    // DTO for receiving the token from client
    public class ExternalLoginRequest
    {
        public string Code { get; set; }
    }
}
