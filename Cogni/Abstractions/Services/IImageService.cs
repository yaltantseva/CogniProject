using Cogni.Models;
using Cogni.Database.Entities;
using Microsoft.AspNetCore.Http;

namespace Cogni.Abstractions.Services
{
    public interface IImageService
    {
        // добавляет изображение на сервер и возвращает URL
        Task<string> UploadImage(IFormFile file);
        Task<(string Url, string Id)> _UploadImage(IFormFile file);
        Task DeleteImage(string id); //удаляет изображение с сервера по ID
    }
}
