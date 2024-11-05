Here’s the complete implementation for the scenario where a user uploads a file via an HTTP trigger, the file is stored in Azure Blob Storage with `FalconRequest` metadata, and a Durable Function periodically checks the metadata. When the metadata indicates the scan is clean, it sends a message to a Service Bus Topic.

### 1. **Orchestration Function** (Polling for Metadata Updates)
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

public static class OrchestratorWithPolling
{
    [FunctionName("OrchestratorWithPolling")]
    public static async Task RunOrchestrator(
        [OrchestrationTrigger] IDurableOrchestrationContext context)
    {
        var blobUrl = context.GetInput<string>();
        Dictionary<string, string> metadata = null;

        while (true)
        {
            metadata = await context.CallActivityAsync<Dictionary<string, string>>("CheckMetadata", blobUrl);

            if (metadata != null && metadata.ContainsKey("ScanStatus"))
            {
                break;
            }

            // Wait before checking again (polling interval)
            var nextCheck = context.CurrentUtcDateTime.AddSeconds(30);
            await context.CreateTimer(nextCheck, CancellationToken.None);
        }

        if (metadata["ScanStatus"] == "Clean")
        {
            await context.CallActivityAsync("SendToServiceBusTopic", blobUrl);
        }
    }
}
```

### 2. **Activity Function to Check Metadata**
```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Azure.Storage.Blobs;

public static class CheckMetadataActivity
{
    [FunctionName("CheckMetadata")]
    public static async Task<Dictionary<string, string>> CheckMetadata([ActivityTrigger] string blobUrl)
    {
        var blobClient = new BlobClient(blobUrl);
        var properties = await blobClient.GetPropertiesAsync();
        return properties.Value.Metadata;
    }
}
```

### 3. **Activity Function to Send Message to Service Bus**
```csharp
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Azure.Messaging.ServiceBus;

public static class SendToServiceBusActivity
{
    [FunctionName("SendToServiceBusTopic")]
    public static async Task SendToServiceBusTopic([ActivityTrigger] string blobUrl)
    {
        var serviceBusConnectionString = Environment.GetEnvironmentVariable("ServiceBusConnectionString");
        var topicName = Environment.GetEnvironmentVariable("ServiceBusTopicName");

        var client = new ServiceBusClient(serviceBusConnectionString);
        var sender = client.CreateSender(topicName);

        var message = new ServiceBusMessage($"File scan complete: {blobUrl}");
        await sender.SendMessageAsync(message);
    }
}
```

### 4. **HTTP Trigger to Start Orchestration with File Upload**
```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Microsoft.Net.Http.Headers;

public static class HttpStartWithFileUpload
{
    [FunctionName("HttpStartWithFileUpload")]
    public static async Task<IActionResult> HttpStartWithFileUpload(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
        [DurableClient] IDurableOrchestrationClient starter,
        ILogger log)
    {
        try
        {
            // Read file and metadata from request
            var formCollection = await req.ReadFormAsync();
            var file = req.Form.Files["file"];
            string falconRequest = req.Form["FalconRequest"];
            string fileName = req.Form["FileName"];

            if (file == null || string.IsNullOrEmpty(falconRequest) || string.IsNullOrEmpty(fileName))
            {
                return new BadRequestObjectResult("Missing file, FalconRequest metadata, or FileName.");
            }

            // Upload the file to Azure Blob Storage
            string blobUrl = await UploadFileToBlobStorage(file, fileName, falconRequest, log);

            // Start the Durable Function orchestration
            string instanceId = await starter.StartNewAsync("OrchestratorWithPolling", blobUrl);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
        catch (Exception ex)
        {
            log.LogError($"Error in HttpStartWithFileUpload: {ex.Message}");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<string> UploadFileToBlobStorage(IFormFile file, string fileName, string falconRequest, ILogger log)
    {
        string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        string containerName = "your-container-name";

        var blobClient = new BlobServiceClient(connectionString);
        var containerClient = blobClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync();

        var blobClientInstance = containerClient.GetBlobClient(fileName);

        var metadata = new Dictionary<string, string>
        {
            { "FalconRequest", falconRequest }
        };

        using (var stream = file.OpenReadStream())
        {
            await blobClientInstance.UploadAsync(stream, overwrite: true);
        }

        await blobClientInstance.SetMetadataAsync(metadata);

        log.LogInformation($"File uploaded to Blob Storage: {blobClientInstance.Uri}");

        return blobClientInstance.Uri.ToString();
    }
}
```

### 5. **Configuration and Setup**

#### **Environment Variables**:
- **AzureWebJobsStorage**: Your Azure Storage connection string.
- **ServiceBusConnectionString**: Your Azure Service Bus connection string.
- **ServiceBusTopicName**: The name of your Azure Service Bus Topic.

#### **host.json**:
Ensure Durable Task extension is configured in `host.json`:
```json
{
  "version": "2.0",
  "extensions": {
    "durableTask": {
      "hubName": "DurableFunctionsHub"
    }
  }
}
```

#### **Blob Container**:
Replace `"your-container-name"` with the actual container name where files will be stored.

### Summary:

- **HTTP Trigger** uploads a file with `FalconRequest` metadata to Azure Blob Storage.
- **Durable Function** polls for metadata updates using `CreateTimer`.
- When metadata indicates a clean scan, it sends a message to a **Service Bus Topic**.
