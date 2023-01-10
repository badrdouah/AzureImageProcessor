using System;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Messaging.EventGrid;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Newtonsoft.Json.Linq;

namespace ImageProcessor
{
    public class Cleaner
    {
        private string connectionString { get; set; }
        private string queueName { get; set; }
        private string containerName { get; set; }
        private string[] blobsResult { get; set; }


        public Cleaner()
        {
            connectionString = Environment.GetEnvironmentVariable("connectionString");
            containerName = Environment.GetEnvironmentVariable("containerName"); ;
            queueName = Environment.GetEnvironmentVariable("queueName"); ;
        }

        [FunctionName("Cleaner")]
        public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo myTimer, ILogger log, ExecutionContext context)
        {
            blobsResult = await ReadStorageQueueMessage(log);

             if (blobsResult != null)
            {
                for (int i = 0; i < blobsResult.Length; i++)
                {
                    var blobName = blobsResult[i].Trim();
                    try
                    {
                        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);

                        // Create a blob client for interacting with the Blob Storage service
                        CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

                        // Get a reference to the container where the blob is located
                        CloudBlobContainer container = blobClient.GetContainerReference(containerName);

                        // Get a reference to the blob
                        var blob = container.GetBlockBlobReference(blobName);
                         await blob.DeleteIfExistsAsync();
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("Something went wrong with blob "+ blobName +" errot :"+ ex.Message);
                    }

                }
                log.LogInformation(blobsResult.Length + " deleted!");
            }
            else
            {
                log.LogInformation("nothing to delete");
            }
        }

        private async Task<string[]> ReadStorageQueueMessage(ILogger log)
        {

            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);

            // Create a queue client
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();

            // Get a reference to the queue
            CloudQueue queue = queueClient.GetQueueReference(queueName);

            // Peek the next message in the queue
            CloudQueueMessage message = await queue.PeekMessageAsync();

            if (message != null)
            {
                // Read the message contents
                string messageContent = message.AsString;

                var blobAgeInMin = DateTime.Now.Subtract(message.InsertionTime.Value.DateTime).TotalMinutes;

                log.LogInformation("blob age = " + blobAgeInMin);

                if (blobAgeInMin < 60)
                {
                    return null;
                }
                else
                {
                    string[] blobsResult = messageContent.Split(',', StringSplitOptions.RemoveEmptyEntries);

                    // Delete the message from the queue
                    message = await queue.GetMessageAsync();
                    await queue.DeleteMessageAsync(message);
                    return blobsResult;

                }
            }
            return null;
        }

    }
}
