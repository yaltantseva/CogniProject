using Cogni.Models;
using Microsoft.AspNetCore.Http;

namespace Cogni.Abstractions.Repositories
{
    public interface IImageRepository
    {
        // добавляет изображение на сервер и возвращает (URL, имя файла)
        Task<(string Url, string Name)> UploadImage(IFormFile file);
        Task DeleteImage(string name); //удаляет изображение с сервера по названию
    }
}
