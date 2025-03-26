
using MEAI.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.OpenApi.Services;
using OpenAI.VectorStores;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace MEAI.Services
{
    public class AIMovieSearchService<T> : IMovieSearchService<T>
    {
        private static bool _initialized = false;

        private readonly ILogger<AIMovieSearchService<T>> _logger;
        private readonly IVectorStore _vectorStore;
        private readonly IServiceProvider _serviceProvider;
        private IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

        public AIMovieSearchService(ILogger<AIMovieSearchService<T>> logger, IVectorStore vectorStore, IServiceProvider serviceProvider,
            [FromKeyedServices("AzureAI")] IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
        { 
            _logger = logger;
            _vectorStore = vectorStore;
            _embeddingGenerator = embeddingGenerator;
            _serviceProvider = serviceProvider;
        }

        public async Task<bool> Refresh(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(GetEmbeddingMetadata(_embeddingGenerator));
            _logger.LogInformation($"Rebuild vectors with: {_embeddingGenerator.ToString()}");

            var movies = _vectorStore.GetCollection<T, MovieVector<T>>("movies");
            await movies.CreateCollectionIfNotExistsAsync();

            var movieData = MovieFactory<T>.GetMovieVectorList();

            foreach (var movie in movieData)
            {
                movie.Vector = await _embeddingGenerator.GenerateEmbeddingVectorAsync(movie.Description);
                await movies.UpsertAsync(movie);
            }

            _initialized = true;

            return true;
        }

        public async Task<IEnumerable<MatchedMovie<T>>> Search(string query, SearchOptions searchOption, CancellationToken cancellationToken = default)
        {
             if (string.IsNullOrWhiteSpace(query))
                return [];

            if (!_initialized)
                await Refresh(cancellationToken);

            var queryEmbedding = await _embeddingGenerator.GenerateEmbeddingVectorAsync(query);

            var movies = _vectorStore.GetCollection<T, MovieVector<T>>("movies");

            var searchOptions = new VectorSearchOptions<MovieVector<T>>()
            {
                Top = searchOption.Top,
                Skip = searchOption.Skip
            };

            var results = await movies.VectorizedSearchAsync(queryEmbedding, searchOptions);
            var matchedMovies = new List<MatchedMovie<T>>();
            await foreach (var result in results.Results)
            {
                matchedMovies.Add(new MatchedMovie<T>() { Key = result.Record.Key, Title = result.Record.Title, Score = result.Score });
            }

            return matchedMovies;
        }

        public async Task<bool> SwitchProvider(string providerName, CancellationToken cancellationToken = default)
        {
            switch (providerName)
            {
                case "AzureAI":
                case "Ollama":
                case "Gemini":
                    var newGenerator = _serviceProvider.GetKeyedService<IEmbeddingGenerator<string, Embedding<float>>>(providerName);
                    if (newGenerator != null)
                    {
                        _embeddingGenerator = newGenerator;
                        await Refresh(cancellationToken);
                    }
                    return true;
                default:
                    _logger.LogWarning($"The provider is not support: {providerName}");
                    return false;
            }
        }

        private string GetEmbeddingMetadata(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("");
            sb.AppendLine("------ Model Info ------");
            var meta = embeddingGenerator.GetService(typeof(EmbeddingGeneratorMetadata)) as EmbeddingGeneratorMetadata;
            if (meta == null)
            {
                Type generatorType = embeddingGenerator.GetType();
                PropertyInfo modelNameProperty = generatorType.GetProperty("Metadata");
                if (modelNameProperty != null)
                {
                    meta = modelNameProperty.GetValue(embeddingGenerator) as EmbeddingGeneratorMetadata;
                }
            }

            if (meta != null)
            {
                sb.AppendLine($"Provider Name: {meta.ProviderName}");
                sb.AppendLine($"Server: {meta.ProviderUri}");
                sb.AppendLine($"Model Name: {meta.ModelId}");
                sb.AppendLine($"Dimensions: {meta.Dimensions}");
            }
            sb.AppendLine("------------------------");

            return sb.ToString();
        }
    }
}
