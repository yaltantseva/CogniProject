using Cogni.Models;
using Cogni.Database.Entities;
using Microsoft.AspNetCore.Http;

namespace Cogni.Abstractions.Services
{
    public interface IImageService
    {
        // добавляет изображение на сервер и возвращает (URL, ID)
        Task<(string Url, string Id)> UploadImage(IFormFile file);
        Task DeleteImage(string id); //удаляет изображение с сервера по ID
    }
}
