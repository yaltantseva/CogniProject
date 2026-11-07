using Cogni.Models;
using Microsoft.AspNetCore.Http;

namespace Cogni.Abstractions.Repositories
{
    public interface IImageRepository
    {
        // добавляет изображение на сервер и возвращает (URL, ID)
        Task<(string Url, string Id)> UploadImage(IFormFile file);
        Task DeleteImage(string fileId); //удаляет изображение с сервера
    }
}
