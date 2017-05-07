using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Web;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace MediaFunctions
{
    public class SubmitMediaIndexer
    {

        // Read values from the App.config file.
        private static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
        private static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");

        static string _storageAccountName = Environment.GetEnvironmentVariable("MediaServicesStorageAccountName");
        static string _storageAccountKey = Environment.GetEnvironmentVariable("MediaServicesStorageAccountKey");

        // Field for service context.
        private static CloudMediaContext _context = null;
        private static MediaServicesCredentials _cachedCredentials = null;
        //private static CloudStorageAccount _destinationStorageAccount = null;

        public static async Task<object> Run(HttpRequestMessage req, TraceWriter log)
        {
            int taskindex = 0;
            bool useEncoderOutputForAnalytics = false;
            IAsset outputEncoding = null;

            log.Info($"Webhook was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            log.Info(jsonContent);

            log.Info($"asset id : {data.assetId}");

            if (data.assetId == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass asset ID in the input object (assetId)"
                });
            }

            log.Info($"Using Azure Media Services account : {_mediaServicesAccountName}");
            IJob job = null;
            int OutputIndex2 = -1;

            try
            {
                // Create and cache the Media Services credentials in a static class variable.
                _cachedCredentials = new MediaServicesCredentials(
                                _mediaServicesAccountName,
                                _mediaServicesAccountKey);

                // Used the chached credentials to create CloudMediaContext.
                _context = new CloudMediaContext(_cachedCredentials);

                // find the Asset
                string assetid = (string)data.assetId;
                IAsset asset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

                if (asset == null)
                {
                    log.Info($"Asset not found {assetid}");
                    return req.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        error = "Asset not found"
                    });
                }

                // Declare a new encoding job with the Standard encoder
                int priority = 10;
                job = _context.Jobs.Create($"Azure Functions Job Submission: {asset.Name}", priority);

                IAsset an_asset = useEncoderOutputForAnalytics ? outputEncoding : asset;


                // Get a media processor reference, and pass to it the name of the processor to use for the specific task.
                IMediaProcessor mediaProcessor = _context.MediaProcessors.Where(p => p.Name == "Azure Media Indexer 2 Preview").ToList().OrderBy(p => new Version(p.Version)).LastOrDefault();

                if (mediaProcessor == null)
                    throw new ArgumentException(string.Format("Unknown media processor", "Azure Media Indexer 2 Preview"));






                string homePath = Environment.GetEnvironmentVariable("HOME", EnvironmentVariableTarget.Process);
                log.Info($"Home path = {homePath}");

                string presetPath;
                if (homePath == String.Empty)
                {
                    presetPath = @"../Presets/" + "IndexerV2.json";
                }
                else
                {
                    // TODO:  Need to make path configurable
                    presetPath = Path.Combine(homePath, @"site\repository\" + @"MediaFunctions\presets\" + "IndexerV2.json");
                }

                string Configuration = File.ReadAllText(presetPath);//  .Replace("EnUs", data.indexV2Language);
                log.Info($"Preset Configuration = {Configuration}");

                // Create a task with the encoding details, using a string preset.
                var task = job.Tasks.AddNew("Azure Media Indexer 2 Preview" + " task", mediaProcessor, Configuration, TaskOptions.None);
                task.Priority = priority;

                // Specify the input asset to be indexed.
                task.InputAssets.Add(an_asset);

                // Add an output asset to contain the results of the job.
                task.OutputAssets.AddNew(an_asset.Name + " " + "Azure Media Indexer 2 Preview" + " Output", AssetCreationOptions.None);
                OutputIndex2 = taskindex++;

           
                job.Submit();
                log.Info("Job Submitted");
            }
            catch (Exception ex)
            {
                log.Info($"Exception {ex}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Error = ex.ToString()
                });
            }

            job = _context.Jobs.Where(j => j.Id == job.Id).FirstOrDefault(); // Let's refresh the job
            log.Info("Job Id: " + job.Id);

            string outputID = null;
            if (OutputIndex2 > -1)
            {
                outputID = job.OutputMediaAssets[OutputIndex2].Id;
            }

            log.Info("OutputAssetIndexV2Id: " + outputID);

            string taskID = null;
            if (OutputIndex2 > -1)
            {
                taskID = job.Tasks[OutputIndex2].Id;
            }

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                jobId = job.Id,
                indexV2 = new
                {
                    assetId = outputID,
                    taskId = taskID,
                    language = (string)data.indexV2Language
                }
            });
        }











    }
}






/*
This function submits a job wth encoding and/or analytics.

Input:
{
    "assetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Mandatory, Id of the source asset
    "mesPreset" : "Adaptive Streaming",         // Optional but required to encode with Media Encoder Standard (MES). If MESPreset contains an extension "H264 Multiple Bitrate 720p with thumbnail.json" then it loads this file from ..\Presets
    "workflowAssetId" : "nb:cid:UUID:2d0d78a2-685a-4b14-9cf0-9afb0bb5dbfc", // Optional, but required to encode the asset with Premium Workflow Encoder. Id for the workflow asset
    "indexV1Language" : "English",              // Optional but required to index the asset with Indexer v1
    "indexV2Language" : "EnUs",                 // Optional but required to index the asset with Indexer v2
    "ocrLanguage" : "AutoDetect" or "English",  // Optional but required to do OCR
    "faceDetectionMode" : "PerFaceEmotion,      // Optional but required to trigger face detection
    "faceRedactionMode" : "analyze",            // Optional, but required for face redaction
    "motionDetectionLevel" : "medium",          // Optional, required for motion detection
    "summarizationDuration" : "0.0",            // Optional. Required to create video summarization. "0.0" for automatic
    "hyperlapseSpeed" : "8",                    // Optional, required to hyperlapse the video
    "priority" : 10,                            // Optional, priority of the job
    "useEncoderOutputForAnalytics" : true       // Optional, use generated asset by MES or Premium Workflow as a source for media analytics
}

Output:
{
    "jobId" :  // job id
    "mes" : // Output asset generated by MES (if mesPreset was specified)
        {
            assetId : "",
            taskId : ""
        },
    "mepw" : // Output asset generated by Premium Workflow Encoder
        {
            assetId : "",
            taskId : ""
        },
    "indexV1" :  // Output asset generated by Indexer v1
        {
            assetId : "",
            taskId : "",
            language : ""
        },
    "indexV2" : // Output asset generated by Indexer v2
        {
            assetId : "",
            taskId : "",
            language : ""
        },
    "ocr" : // Output asset generated by OCR
        {
            assetId : "",
            taskId : ""
        },
    "faceDetection" : // Output asset generated by Face detection
        {
            assetId : ""
            taskId : ""
        },
    "faceRedaction" : // Output asset generated by Face redaction
        {
            assetId : ""
            taskId : ""
        },
     "motionDetection" : // Output asset generated by motion detection
        {
            assetId : "",
            taskId : ""
        },
     "summarization" : // Output asset generated by video summarization
        {
            assetId : "",
            taskId : ""
        },
     "hyperlapse" : // Output asset generated by Hyperlapse
        {
            assetId : "",
            taskId : ""
        }
 }
*/


