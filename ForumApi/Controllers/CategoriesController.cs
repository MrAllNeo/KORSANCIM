using ForumApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForumApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoriesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/categories — arayüz kategori listesini buradan çeker,
        // isimler artık HTML içinde sabit kodlu değil.
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.Categories
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.Slug,
                    c.Description,
                    c.Icon,
                    TopicCount = c.Topics.Count()
                })
                .ToListAsync();

            return Ok(categories);
        }
    }
}
