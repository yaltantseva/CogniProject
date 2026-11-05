using Cogni.Database.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cogni.Abstractions.Repositories;
using Cogni.Abstractions.Services;
using Cogni.Models;
using Cogni.Contracts.Requests;
using Cogni.Contracts.Responses;
using Humanizer;

namespace Cogni.Services
{
    public class ArticleService : IArticleService
    {
        private readonly IArticleRepository _articleRepository;
        private readonly IImageService _imageService;
        private readonly IUserService _userService;
        private readonly ILogger<ArticleService> _logger;

        public ArticleService(IArticleRepository articleRepository, IImageService imageService, IUserService userService, ILogger<ArticleService> logger)
        {
            _logger = logger;
            _articleRepository = articleRepository;
            _imageService = imageService;
            _userService = userService;
        }

        public async Task<List<ArticlePreviewResponse>> GetArticlesPreviewesAsync()
        {
            // Получаем статьи сразу с авторами и их MBTI
            var articles = await _articleRepository.GetAllWithAuthors();

            List<ArticlePreviewResponse> articlePreviewResponses = new List<ArticlePreviewResponse>();

            foreach (var article in articles)
            {
                if (article == null || article.IdUserNavigation == null)
                {
                    continue; // Пропускаем, если статья пустая или автор не найден
                }

                var user = article.IdUserNavigation;

                // Вычисление времени с момента публикации
                var timeSincePublication = article.Created.HasValue
                    ? article.Created.Value.ToLocalTime().Humanize(culture: new System.Globalization.CultureInfo("ru-RU"))
                    : "Неизвестно";

                var r = new ArticlePreviewResponse(
                    article.Id,
                    article.IdUser,
                    article.ArticlePreview,
                    null, // Картинки в User нет, передаем null
                    user.Name, // Имя автора
                    article.ReadsNumber ?? 0,
                    timeSincePublication,
                    
                    // Достаем MBTI из навигации. Если её нет, то null
                    user.IdMbtiTypeNavigation != null ? user.IdMbtiTypeNavigation.NameOfType : null, 
                    
                    article.ArticleName,
                    article.Annotation
                );
                
                articlePreviewResponses.Add(r);
            }
            
            return articlePreviewResponses;
        }

        public async Task<List<ArticleModel>> GetAllAsync()
        {
            var articles = await _articleRepository.GetAll();
            return articles.Select(article => ToModel(article)).ToList();
        }

        public async Task<List<(int Id, string? ArticleName)>> GetAllArticleIdsAndNamesAsync()
        {
            var articles = await _articleRepository.GetAll();
            return articles.Select(article => (article.Id, article.ArticleName)).ToList();
        }

        public async Task<ArticleModel> GetArticleByIdAsync(int id)
        {
            var article = await _articleRepository.GetById(id);
            return article != null ? ToModel(article) : null;
        }

        public async Task<Article> CreateArticleAsync(CreateArticleRequest request, int userId)
        {
            string? articlePreviewUrl = null;

            if (request.ArticlePreviewFile != null)
            {
                articlePreviewUrl = await _imageService.UploadImage(request.ArticlePreviewFile);
            }
            var article = new Article
            {
                ArticleName = request.ArticleName,
                ArticleBody = request.ArticleBody,
                IdUser = userId,
                Annotation = request.Annotation,
                ArticleImages = new List<ArticleImage>(),
                ArticlePreview = articlePreviewUrl
            };

            if (request.Files != null)
            {
                foreach (var file in request.Files)
                {
                    var imageUrl = await _imageService.UploadImage(file);
                    article.ArticleImages.Add(new ArticleImage { ImageUrl = imageUrl });
                }
            }

            List<string> imageUrls = article.ArticleImages.Select(ai => ai.ImageUrl).ToList();

            Article createdArticle = await _articleRepository.Create(article.ArticleName, article.ArticleBody, imageUrls, article.IdUser, article.Annotation, article.ArticlePreview);

            return createdArticle;
        }



        public async Task<ArticleModel> UpdateArticleAsync(int id, ArticleUpdateRequest request, int userId)
        {
            var article = await _articleRepository.GetById(id);

            if (article == null)
            {
                throw new ArgumentException("Статья не найдена."); // Заменили Exception на ArgumentException для 404
            }

            // ДОБАВЛЕНА ПРОВЕРКА: если это не автор и не админ
            // (Проверку на админа мы сюда не передаем, но если нужно, можно добавить bool isAdmin)
            if (article.IdUser != userId)
            {
                throw new UnauthorizedAccessException("Вы не можете редактировать чужие статьи.");
            }

            string? articlePreviewUrl = article.ArticlePreview;
            if (request.ArticlePreviewFile != null)
            {
                articlePreviewUrl = await _imageService.UploadImage(request.ArticlePreviewFile);
            }

            // Обновляем название и содержимое статьи
            article.ArticleName = request.ArticleName;
            article.ArticleBody = request.ArticleBody;
            article.Annotation = request.Annotation;
            article.ArticlePreview = articlePreviewUrl;

            // Получаем существующие URL изображений
            List<string> imageUrls = article.ArticleImages.Select(ai => ai.ImageUrl).ToList();

            // Обрабатываем загруженные изображения
            if (request.Files != null)
            {
                foreach (var file in request.Files)
                {
                    var imageUrl = await _imageService.UploadImage(file);
                    imageUrls.Add(imageUrl);
                    article.ArticleImages.Add(new ArticleImageModel { ImageUrl = imageUrl });
                }
            }

            // Обновляем статью в репозитории
            await _articleRepository.Update(article.Id, article.ArticleName, article.ArticleBody, imageUrls, request.Annotation, article.ArticlePreview);

            // Возвращаем обновленную статью
            return article;
        }

        public async Task IncrementArticleReadsAsync(int id)
        {
            var article = await _articleRepository.GetById(id); // Получаем статью из репозитория

            if (article == null)
            {
                throw new Exception("Статья не найдена."); // Или другое подходящее исключение
            }


            article.ReadsNumber = (article.ReadsNumber ?? 0) + 1; // Увеличиваем счетчик (если null, начинаем с 0)

            // Сохраняем изменения в репозитории (предполагается, что такой метод существует)
            await _articleRepository.UpdateReadsNumber(article.Id, article.ReadsNumber.Value); // Нужно реализовать в репозитории
        }

        public async Task DeleteArticleAsync(int id, int currentUserId, bool isAdmin)
        {
            // 1. Загружаем статью, чтобы узнать её автора
            var article = await _articleRepository.GetById(id);

            if (article == null)
            {
                throw new ArgumentException("Статья не найдена.");
            }

            // 2. Проверяем права: если это не автор и не админ — запрещаем
            if (article.IdUser != currentUserId && !isAdmin)
            {
                throw new UnauthorizedAccessException("Вы не можете удалять чужие статьи.");
            }

            // 3. Если всё ок — удаляем
            await _articleRepository.Delete(id);
        }
        private ArticleModel ToModel(ArticleModel article)
        {
            return new ArticleModel
            {
                Id = article.Id,
                ArticleName = article.ArticleName,
                ArticleBody = article.ArticleBody,
                IdUser = article.IdUser,
                Annotation = article.Annotation,
                Created = article.Created,
                ReadsNumber = article.ReadsNumber,
                ArticlePreview = article.ArticlePreview,
                ArticleImages = article.ArticleImages.Select(ai => new ArticleImageModel
                {
                    Id = ai.Id,
                    ArticleId = ai.ArticleId,
                    ImageUrl = ai.ImageUrl
                }).ToList(),
                IdUserNavigation = article.IdUserNavigation
            };
        }
    }
}