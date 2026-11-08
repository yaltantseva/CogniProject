using Minio;
using Minio.DataModel.Args;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.Versioning;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
namespace ChatService.Controllers;

[Authorize]
[Route("Chat/Files")]
public class FilesController : ControllerBase
{
    private readonly IMinioClient _minioClient;
    private const string BucketName = "chat-files";
    private static readonly HashSet<string> BlockedExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".exe",
        ".sh",
        ".bat",
        ".html",
        ".svg"
    };
    public FilesController(IMinioClientFactory minioFactory)
    {
        _minioClient = minioFactory.CreateClient();
    }

    [HttpPost("Upload")]
    public async Task<IActionResult> UploadFile([FromForm] List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest("No files provided.");
        }
        if (files.Count > 9)
        {
            return BadRequest("You can upload a maximum of 9 files.");
        }
        const long maxSize = 50L * 1024 * 1024;
        var uploadedFiles = new List<string>(); 
        foreach (var file in files)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Empty files are not allowed.");
            }
            if (file.Length > maxSize)
            {
                return BadRequest("File size must not exceed 50 MB.");
            }
            var fileExtension = Path.GetExtension(file.FileName);
            if (BlockedExtensions.Contains(fileExtension))
            {
                return BadRequest($"Files with extension {fileExtension} are not allowed.");
            }
        }
        foreach (var file in files)
        {
            var originalFileName = file.FileName;
            var fileExtension = Path.GetExtension(originalFileName);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
            var normalizedFileName = $"{fileNameWithoutExtension}{fileExtension.ToLowerInvariant()}";
            var uniqueFileName = $"{Guid.NewGuid()}_{normalizedFileName}";

            using var fileStream = file.OpenReadStream();

            await _minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(BucketName)
                .WithObject(uniqueFileName)
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType("application/octet-stream")
            ); 
            uploadedFiles.Add($"/{BucketName}/{uniqueFileName}");           
        }
        return Ok(new {links = uploadedFiles});
    }
}
