using Cogni.Abstractions.Repositories;
using Minio;
using Minio.DataModel.Args;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Cogni.Database.Repositories
{
    public class ImageRepository : IImageRepository
    {
        private const string BUCKET_NAME = "cogni-media";
        private readonly IMinioClient _minioClient;
        private readonly string _publicUrl;

        public ImageRepository(
            IConfiguration config,
            IMinioClientFactory minioFactory
        ) {
            _minioClient = minioFactory.CreateClient();
            _publicUrl = config["Minio:PublicUrl"]?.TrimEnd('/') ??
            throw new InvalidOperationException(
                "Не задан параметр конфигурации Minio:PublicUrl"
            );
        }

        public async Task<(string Url, string Name)> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("Файл не выбран или пуст");
            
            var extension = file.ContentType?.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                _ => throw new InvalidDataException(
                    "Поддерживаются только JPG и PNG"
                )
            };

            var objName = $"{Guid.NewGuid():N}{extension}";
            using var stream = file.OpenReadStream();
            await _minioClient.PutObjectAsync(
                new PutObjectArgs()
                .WithBucket(BUCKET_NAME)
                .WithObject(objName)
                .WithStreamData(stream)
                .WithObjectSize(file.Length)
                .WithContentType(file.ContentType)
            );

            var url = $"{_publicUrl}/{BUCKET_NAME}/{objName}";
            return (url, objName);
        }
        
        // Удаляет изображение с сервера по названию
        public async Task DeleteImage(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("ID файла не задан");
            await _minioClient.RemoveObjectAsync(
                new RemoveObjectArgs()
                .WithBucket(BUCKET_NAME)
                .WithObject(name)
            );
        }
    }
}
