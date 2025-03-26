using MEAI.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace MEAI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MoviesController(ILogger<MoviesController> logger, IMovieSearchService<int> searchService) : ControllerBase
    {
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string query = "A family friendly movie that includes ogres and dragons", int top = 3, CancellationToken cancellationToken = default)
        {
            var results = await searchService.Search(query, new SearchOptions { Top = top }, cancellationToken);
            if (results == null)
                return Ok("No Movie matched");

            StringBuilder sb = new StringBuilder();
            foreach (var result in results)
            {
                sb.AppendLine($"Key: {result.Key}, Title: {result.Title}, Score: {result.Score}");
            }

            return Ok(sb.ToString());
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(CancellationToken cancellationToken = default)
        {
            await searchService.Refresh(cancellationToken);

            return Ok();
        }

        /// <summary>
        /// Switch AI Embedding provider
        /// </summary>
        /// <param name="providerName" example="Gemini, AzureAI, Ollama"></param>
        /// <param name="cancellationToken"></param>
        /// <response code="200"></response>
        [HttpPost("switch/{providerName}")]
        public async Task<IActionResult> SwitchProvider([FromRoute] string providerName, CancellationToken cancellationToken = default)
        {
           var result = await searchService.SwitchProvider(providerName, cancellationToken);

            return Ok(result);
        }
    }
}
