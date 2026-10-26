using Cogni.Abstractions.Repositories;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Cogni.Database.Repositories
{
    public class ImageRepository : IImageRepository
    {
        private string UPLOADCARE_PUB_KEY;
        private string UPLOADCARE_SECRET_KEY;

        // Добавляет изображение на сервер и возвращает URL с ID

        public ImageRepository(IConfiguration config) 
        {

            var pub_key = config["ImageRepository:UPLOADCARE_PUB_KEY"];
            UPLOADCARE_PUB_KEY = pub_key?.Trim() ?? string.Empty;
            if (String.IsNullOrWhiteSpace(UPLOADCARE_PUB_KEY)) {
                throw new InvalidOperationException(
                    "Не задана конфигурация " +
                    "'ImageRepository:UPLOADCARE_PUB_KEY'."
                );
            }
            else if (!Regex.IsMatch(UPLOADCARE_PUB_KEY, @"^[a-z0-9]+$"))
            {
                throw new InvalidOperationException(
                    "Неверный формат " +
                    "'ImageRepository:UPLOADCARE_PUB_KEY'."
                );
            }

            var sec_key = config["ImageRepository:UPLOADCARE_SECRET_KEY"];
            UPLOADCARE_SECRET_KEY = sec_key?.Trim() ?? string.Empty;
            if (String.IsNullOrWhiteSpace(UPLOADCARE_SECRET_KEY)) {
                throw new InvalidOperationException(
                    "Не задана конфигурация " +
                    "'ImageRepository:UPLOADCARE_SECRET_KEY'."
                );
            }
            else if (!Regex.IsMatch(UPLOADCARE_SECRET_KEY, @"^[a-z0-9]+$"))
            {
                throw new InvalidOperationException(
                    "Неверный формат " +
                    "'ImageRepository:UPLOADCARE_SECRET_KEY'."
                );
            }
        }

        public async Task<String> UploadImage(IFormFile file)
        {
            using var client = new HttpClient();
            using var formData = new MultipartFormDataContent();
            using var fileContent = new StreamContent(file.OpenReadStream());
            formData.Add(fileContent, "file", file.FileName);
            formData.Add(new StringContent(UPLOADCARE_PUB_KEY), "UPLOADCARE_PUB_KEY");
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            var response = await client.PostAsync(
                "https://upload.uploadcare.com/base/",
                formData
            );
            if (!response.IsSuccessStatusCode)
                throw new Exception("Ошибка при загрузке изображения");
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var fileId = JsonDocument.Parse(responseContent).RootElement.GetProperty("file").GetString();
            return $"https://ucarecdn.com/{fileId}/";
        }
        // Удаляет изображение с сервера по ID
        public async Task DeleteImage(string fileId)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Uploadcare.Simple {UPLOADCARE_PUB_KEY}:{UPLOADCARE_SECRET_KEY}");
            var response = await client.DeleteAsync($"https://api.uploadcare.com/files/{fileId}/");
            if (!response.IsSuccessStatusCode)
                throw new Exception("Ошибка при удалении изображения");
        }
    }
}
