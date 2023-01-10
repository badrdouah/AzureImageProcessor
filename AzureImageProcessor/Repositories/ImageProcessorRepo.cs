using System;
using System.Reflection;
using Azure;
using Azure.Messaging.EventGrid;
using ImageProcessorWebApp.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ImageProcessorWebApp.Repositories
{
    public interface IImageProcessorRepo
    {
        Task<List<string>> ProcessUploadImages(IFormFile file, imageType _imageType, orientation _orientation);
        List<ResolutionModel> GetResolutions();
    }

    public class ImageProcessorRepo : IImageProcessorRepo
    {
        string connectionString { get; set; }
        string containerName { get; set; }
        string queueName { get; set; }
        string topicEndpoint { get; set; }
        string topicKey { get; set; }
        public List<ResolutionModel> resolutions { get; set; }

        IConfiguration Configuration { get; set; }

        public ImageProcessorRepo(IConfiguration configuration)
        {
            Configuration = configuration;
            connectionString = Configuration["connectionString"];
            containerName = Configuration["containerName"];
            queueName = Configuration["queueName"];
            topicEndpoint = Configuration["topicEndpoint"];
            topicKey = Configuration["topicKey"];


            resolutions = new List<ResolutionModel>();
            resolutions.Add(new ResolutionModel() { width = 2778, height = 1284 });
            resolutions.Add(new ResolutionModel() { width = 2688, height = 1242 });
            resolutions.Add(new ResolutionModel() { width = 2532, height = 1170 });
            resolutions.Add(new ResolutionModel() { width = 2436, height = 1125 });
            resolutions.Add(new ResolutionModel() { width = 2340, height = 1080 });
            resolutions.Add(new ResolutionModel() { width = 2208, height = 1242 });
            resolutions.Add(new ResolutionModel() { width = 1136, height = 750 });
            resolutions.Add(new ResolutionModel() { width = 1136, height = 640 });
            resolutions.Add(new ResolutionModel() { width = 960, height = 600 });
            resolutions.Add(new ResolutionModel() { width = 960, height = 640 });
        }

        public async Task<List<string>> ProcessUploadImages(IFormFile file, imageType _imageType, orientation _orientation)
        {
            var imageExtention = file.ContentType.Split('/')[1];
            // model.ImageType = imageType.Png;

            if (_imageType == imageType.Jpeg) imageExtention = "Jpeg";
            else if (_imageType == imageType.Webp) imageExtention = "Webp";
            else if (_imageType == imageType.Png) imageExtention = "Png";


            var blobName = Path.GetRandomFileName() + "." + imageExtention;

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);

            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            CloudBlockBlob blob = container.GetBlockBlobReference(blobName);

            using (var stream = file.OpenReadStream())
            {
                await blob.UploadFromStreamAsync(stream);
            }
            var resultBlobsName = new List<string>();
            var resultBlobsUrls = new List<string>();
            try
            {
                using (var stream = await blob.OpenReadAsync())
                {
                    // Use ImageSharp to resize the image
                    using (Image image = Image.Load(stream))
                    {
                        for (int i = 0; i < resolutions.Count; i++)
                        {
                            var width = (_orientation == orientation.landscape) ? resolutions[i].width : resolutions[i].height;
                            var height = (_orientation == orientation.landscape) ? resolutions[i].height : resolutions[i].width;
                            var editedImagePath = await ProcessImage(container, image, _imageType, blob.Name, width, height);

                            //upload new edited image 
                            CloudBlockBlob newblob = container.GetBlockBlobReference(width + "x" + height + "_" + blob.Name);

                            await newblob.UploadFromFileAsync(editedImagePath);

                            //fill event data
                            resultBlobsUrls.Add(newblob.StorageUri.PrimaryUri.AbsoluteUri);
                            resultBlobsName.Add(newblob.Name);

                            //clean up
                            System.IO.File.Delete(editedImagePath);
                        }
                    }
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                // Let the user know that the directory does not exist
                Console.WriteLine($"Something went wrong : {ex.Message}");
            }



            //send a queue message to a queue in same storage account ,
            //so it can be cleaned by the clean azure function that will trigger in 10 mins after every process 
            await SendStorageQueueMessage(resultBlobsName);


            return resultBlobsUrls;
        }

        public List<ResolutionModel> GetResolutions()
        {
            return resolutions;
        }

        private async Task<string> ProcessImage(CloudBlobContainer container, Image image, imageType imageType,
            string blobName, int width, int height)
        {
            var editedImagePath = Path.Combine(Path.GetTempPath(), width + "x" + height + blobName);

            //resize image
            image.Mutate(x => x.Resize(width, height));
            //save resized image to disc

            if (imageType == imageType.Jpeg) await image.SaveAsJpegAsync(editedImagePath);
            else if (imageType == imageType.Webp) await image.SaveAsWebpAsync(editedImagePath);
            else if (imageType == imageType.Png) await image.SaveAsPngAsync(editedImagePath);
            else await image.SaveAsync(editedImagePath);

            return editedImagePath;
        }

        private async Task SendStorageQueueMessage(List<string> list)
        {

            // Replace "STORAGE_CONNECTION_STRING" with your storage connection string
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);

            // Create a queue client
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();

            // Get a reference to the queue
            CloudQueue queue = queueClient.GetQueueReference(queueName);

            // Get the next message in the queue
            CloudQueueMessage message = new CloudQueueMessage(string.Join(", ", list));


            await queue.AddMessageAsync(message);
        }

        private async Task TriggerStorageCleanEvent()
        {
            Uri endpoint = new Uri(topicEndpoint);
            AzureKeyCredential credential = new AzureKeyCredential(topicKey);
            EventGridPublisherClient client = new EventGridPublisherClient(endpoint, credential);
            // Create an event data object

            EventGridEvent eventData = new EventGridEvent(
                subject: "Trigger clean container storage function",
                eventType: "Clean",
                dataVersion: "1.0",
                data: new { }
            );


            await client.SendEventAsync(eventData);
        }
    }
}

