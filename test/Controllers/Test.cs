using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using test.Configuration;

namespace test.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class Test : ControllerBase
    {
        private string _storage1ConnectionString;
        private string _storage2ConnectionString;
        private string _blobContainerName;

        public Test(IOptionsMonitor<StorageTest> options)
        {
            void SetFields(StorageTest current)
            {
                _storage1ConnectionString = current.Storage1ConnectionString;
                _storage2ConnectionString = current.Storage2ConnectionString;
                _blobContainerName = current.ContainerName;
            }
            options.Configure(SetFields);
        }

        [HttpGet]
        public async Task<string> Get(string name, CancellationToken cancellationToken)
        {
            var answer1 = await Download(_storage1ConnectionString, _blobContainerName, name, cancellationToken);
            var answer2 = await Download(_storage2ConnectionString, _blobContainerName, name, cancellationToken);
            return $"{FormatMessage("Storage1", answer1)}{Environment.NewLine}{FormatMessage("Storage2", answer2)}";
        }

        [HttpPost]
        public async Task<string> Post(string name, string value, CancellationToken cancellationToken)
        {
            var save1 = await Upload(_storage1ConnectionString, _blobContainerName, name, value, cancellationToken);
            var save2 = await Upload(_storage2ConnectionString, _blobContainerName, name, value, cancellationToken);
            return $"{FormatMessage("Storage1", save1)}{Environment.NewLine}{FormatMessage("Storage2", save2)}";
        }

        [HttpDelete]
        public async Task<string> Delete(CancellationToken cancellationToken)
        {
            var delete1 = await Delete(_storage1ConnectionString, _blobContainerName, cancellationToken);
            var delete2 = await Delete(_storage2ConnectionString, _blobContainerName, cancellationToken);
            return $"{FormatMessage("Storage1", delete1)}{Environment.NewLine}{FormatMessage("Storage2", delete2)}";
        }

        private static string FormatMessage(string name, (string, Exception) data)
        {
            var (message, exception) = data;
            if (exception == null)
            {
                return $"{name}: {message}";
            }

            var len = name.Length + 2;
            var newLine = $"{Environment.NewLine}{new string(' ', len)}";
            return $"{name}: {message}{newLine}{exception.Message}{newLine}{exception.StackTrace}";
        }

        private static async Task<(string message, Exception exception)> Delete(string connectionString, string containerName, CancellationToken cancellationToken)
        {
            try
            {
                var containerClient = new BlobContainerClient(connectionString, containerName);
                if (await containerClient.ExistsAsync(cancellationToken))
                {
                    await containerClient.DeleteAsync(cancellationToken: cancellationToken);
                }

                return ("Succeeded", null);
            }
            catch (Exception e)
            {
                return ("Failed", e);
            }
        }

        private static async Task<(string message, Exception exception)> Upload(string connectionString, string containerName, string name, string value, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("You have to give me something, yo.", nameof(name));
                }

                var containerClient = new BlobContainerClient(connectionString, containerName);
                if (!(await containerClient.ExistsAsync(cancellationToken)).Value)
                {
                    await containerClient.CreateAsync(cancellationToken: cancellationToken);
                }

                var client = new BlobClient(connectionString, containerName, name);
                var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(value));
                memoryStream.Seek(0, SeekOrigin.Begin);
                await client.UploadAsync(memoryStream, true, cancellationToken);
                return ("Succeeded", null);
            }
            catch (Exception e)
            {
                return ("Failed", e);
            }
        }

        private static async Task<(string message, Exception exception)> Download(string connectionString, string containerName, string name, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("You have to give me something, yo.", nameof(name));
                }

                var client = new BlobClient(connectionString, containerName, name);
                if (!(await client.ExistsAsync(cancellationToken)).Value)
                {
                    return ("**MISSING**", null);
                }

                await using var stream = await client.OpenReadAsync(cancellationToken: cancellationToken);
                using var reader = new StreamReader(stream);
                return (await reader.ReadToEndAsync(), null);
            }
            catch (Exception e)
            {
                return ($"Error connecting to \"{connectionString}/{containerName}/{name}\"", e);
            }
        }
    }
}