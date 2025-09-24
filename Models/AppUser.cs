using Microsoft.AspNetCore.Identity;

namespace LectureCompanion.Api.Models
{
    public class AppUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;

        // Later you can add Audios, Transcripts, etc. as navigation properties
    }
}
