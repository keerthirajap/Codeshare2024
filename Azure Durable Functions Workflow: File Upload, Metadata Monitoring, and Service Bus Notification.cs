Detailed Workflow Explanation
The solution involves a step-by-step process to handle file uploads, monitor for metadata updates, and send a notification once the scan is complete. Here’s how each component works:

1. HTTP Trigger Function - Upload File and Start Orchestration
Step-by-Step Process:
Receive File and Metadata:

The user sends a POST request with a file (IFormFile), FalconRequest metadata, and FileName.
Upload File to Blob Storage:

The file is uploaded to Azure Blob Storage in a specified container.
Metadata, including FalconRequest, is attached to the uploaded file.
Start Durable Function:

The function initiates a Durable Function orchestration (OrchestratorWithPolling) by passing the Blob URL.
It returns a status response containing the orchestration instance ID for tracking.
2. Durable Function Orchestration - Polling for Metadata Updates
Step-by-Step Process:
Receive Blob URL:

The Durable Function orchestration starts with the Blob URL provided by the HTTP trigger.
Periodic Check for Metadata:

The function enters a loop, periodically invoking the CheckMetadata activity function.
It waits 30 seconds between checks using CreateTimer.
Check Metadata:

The CheckMetadata function retrieves the file's metadata from Blob Storage.
It looks for a ScanStatus field in the metadata.
Exit Condition:

The loop breaks when ScanStatus is found in the metadata.
Send Notification:

If ScanStatus is "Clean," the function triggers SendToServiceBusTopic to send a message.
3. Activity Function - Check Metadata
Step-by-Step Process:
Retrieve Blob Metadata:
The CheckMetadata function uses the Blob URL to access the file in Blob Storage.
It retrieves and returns the file’s metadata, focusing on the ScanStatus field.
4. Activity Function - Send Message to Service Bus
Step-by-Step Process:
Prepare and Send Message:
The SendToServiceBusTopic function creates a message indicating the file scan is complete.
It sends this message to a predefined Service Bus Topic using the connection string and topic name.
5. Configuration and Setup
Environment Variables:
AzureWebJobsStorage: Connection string for Azure Blob Storage.
ServiceBusConnectionString: Connection string for Azure Service Bus.
ServiceBusTopicName: The name of the Service Bus Topic.
host.json Configuration:
Specifies settings for the Durable Task extension, like the hub name used to track orchestration state.
Blob Container:
Ensure the Blob Storage container name is correctly set and accessible.
Workflow Summary:
User Uploads a File:

Via an HTTP trigger, the file and metadata are uploaded to Blob Storage.
Start Durable Orchestration:

Begins polling for updates to the file’s metadata.
Periodic Metadata Check:

Every 30 seconds, it checks for the ScanStatus metadata update.
Send Notification:

Once ScanStatus is "Clean," a message is sent to the Service Bus Topic to signal the file is ready for further processing or use.
This workflow ensures efficient file handling, automated monitoring, and reliable notification delivery once the file is scanned and verified.
    
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
