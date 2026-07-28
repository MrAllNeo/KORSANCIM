using ForumApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Controllers
{
    // Herkese açık okuma ucu — arayüz her sayfada bakım modu/duyuru/kayıt
    // durumunu bunun üzerinden kontrol eder. Yazma AdminController'da
    // (PUT api/admin/settings, yalnızca Owner).
    [ApiController]
    [Route("api/settings")]
    public class SettingsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SettingsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            var settings = await _context.SiteSettings.FindAsync(1);

            return Ok(new
            {
                registrationOpen = settings?.RegistrationOpen ?? true,
                maintenanceMode = settings?.MaintenanceMode ?? false,
                announcementText = settings?.AnnouncementText
            });
        }
    }
}
