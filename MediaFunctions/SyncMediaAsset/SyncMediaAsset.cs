﻿using System;
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
    public class SyncMediaAsset
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

            log.Info($"Webhook was triggered!");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            log.Info(jsonContent);

            if (data.assetId == null)
            {
                
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass assetId in the input object"
                });
            }


            log.Info($"Using Azure Media Services account : {_mediaServicesAccountName}");

            try
            {
                // Create and cache the Media Services credentials in a static class variable.
                _cachedCredentials = new MediaServicesCredentials(
                                _mediaServicesAccountName,
                                _mediaServicesAccountKey);

                // Used the chached credentials to create CloudMediaContext.
                _context = new CloudMediaContext(_cachedCredentials);

                // Step 1:  Copy the Blob into a new Input Asset for the Job
                // ***NOTE: Ideally we would have a method to ingest a Blob directly here somehow. 
                // using code from this sample - https://azure.microsoft.com/en-us/documentation/articles/media-services-copying-existing-blob/

                // Get the asset
                string assetid = data.assetId;
                var asset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();

                if (asset == null)
                {
                    log.Info($"Asset not found {assetid}");

                    return req.CreateResponse(HttpStatusCode.BadRequest, new
                    {
                        error = "Asset not found"
                    });
                }

                log.Info("Asset found, ID: " + asset.Id);

                //Get a reference to the storage account that is associated with the Media Services account. 
                StorageCredentials mediaServicesStorageCredentials =
                    new StorageCredentials(_storageAccountName, _storageAccountKey);
                var _destinationStorageAccount = new CloudStorageAccount(mediaServicesStorageCredentials, false);

                CloudBlobClient destBlobStorage = _destinationStorageAccount.CreateCloudBlobClient();

                // Get the destination asset container reference
                string destinationContainerName = asset.Uri.Segments[1];
                log.Info($"destinationContainerName : {destinationContainerName}");

                CloudBlobContainer assetContainer = destBlobStorage.GetContainerReference(destinationContainerName);
                log.Info($"assetContainer retrieved {assetContainer.Name}");

                // Get hold of the destination blobs
                var blobs = assetContainer.ListBlobs();
                log.Info($"blobs retrieved {blobs.Count()}");


                foreach (CloudBlockBlob blob in blobs)
                {
                    var assetFile = asset.AssetFiles.Create(blob.Name);
                    assetFile.ContentFileSize = blob.Properties.Length;
                    //assetFile.IsPrimary = true;
                    assetFile.Update();
                    log.Info($"Asset file updated : {assetFile.Name}");

                }

                asset.Update();

                log.Info("Asset updated");
            }
            catch (Exception ex)
            {
                log.Info($"Exception {ex}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Error = ex.ToString()
                });
            }


            return req.CreateResponse(HttpStatusCode.OK);














            ////*************************************************************************************


            //log.Info($"Webhook was triggered!");

            //string jsonContent = await req.Content.ReadAsStringAsync();
            //dynamic data = JsonConvert.DeserializeObject(jsonContent);

            //log.Info(jsonContent);

            //if (data.assetId == null)
            //{

            //    return req.CreateResponse(HttpStatusCode.BadRequest, new
            //    {
            //        error = "Please pass assetId in the input object"
            //    });
            //}


            //log.Info($"Using Azure Media Services account : {_mediaServicesAccountName}");

            //try
            //{
            //    // Create and cache the Media Services credentials in a static class variable.
            //    _cachedCredentials = new MediaServicesCredentials(
            //                    _mediaServicesAccountName,
            //                    _mediaServicesAccountKey);

            //    // Used the chached credentials to create CloudMediaContext.
            //    _context = new CloudMediaContext(_cachedCredentials);

            //    // Step 1:  Copy the Blob into a new Input Asset for the Job
            //    // ***NOTE: Ideally we would have a method to ingest a Blob directly here somehow. 
            //    // using code from this sample - https://azure.microsoft.com/en-us/documentation/articles/media-services-copying-existing-blob/

            //    // Get the asset
            //    string assetid = data.assetId;
            //    IAsset asset = _context.Assets.Where(a => a.Id == assetid).FirstOrDefault();


            //    if (asset == null)
            //    {
            //        log.Info($"Asset not found {assetid}");

            //        return req.CreateResponse(HttpStatusCode.BadRequest, new
            //        {
            //            error = "Asset not found"
            //        });
            //    }

            //    log.Info("Asset found, ID: " + asset.Id);


            //    // ##############################


            //    IAccessPolicy writePolicy = _context.AccessPolicies.Create("writePolicy", TimeSpan.FromHours(24), AccessPermissions.Write);
            //    ILocator destinationLocator =
            //    _context.Locators.CreateLocator(LocatorType.Sas, asset, writePolicy);


            //    //Get a reference to the storage account that is associated with the Media Services account. 
            //    StorageCredentials mediaServicesStorageCredentials =
            //        new StorageCredentials(_storageAccountName, _storageAccountKey);
            //    var _destinationStorageAccount = new CloudStorageAccount(mediaServicesStorageCredentials, false);

            //    CloudBlobClient destBlobStorage = _destinationStorageAccount.CreateCloudBlobClient();

            //    // Get the destination asset container reference
            //    //string destinationContainerName = asset.Uri.Segments[1];
            //    string destinationContainerName = (new Uri(destinationLocator.Path)).Segments[1];
            //    log.Info($"destinationContainerName : {destinationContainerName}");

            //    CloudBlobContainer assetContainer = destBlobStorage.GetContainerReference(destinationContainerName);
            //    log.Info($"assetContainer retrieved");

            //    if (assetContainer.CreateIfNotExists())
            //    {
            //        assetContainer.SetPermissions(new BlobContainerPermissions
            //        {
            //            PublicAccess = BlobContainerPublicAccessType.Blob
            //        });
            //    }

            //    CloudBlockBlob blob = assetContainer.GetBlockBlobReference(asset.Name);
            //    // blob.FetchAttributes();
            //    log.Info($"blobs retrieved {blob.Uri} {blob.Name} {blob.Properties.Length}");
            //    var assetFile = asset.AssetFiles.Create((blob as ICloudBlob).Name);

            //        var signature = blob.GetSharedAccessSignature(new SharedAccessBlobPolicy
            //        {
            //            Permissions = SharedAccessBlobPermissions.Read,
            //            SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(10)
            //        });

            //        CloudBlockBlob destinationBlob = assetContainer.GetBlockBlobReference(blob.Name);


            //         //await   CopyBlobHelpers.CopyBlobAsync(blob, destinationBlob, new BlobRequestOptions(), CancellationToken.None);





            //    destinationLocator.Delete();
            //    writePolicy.Delete();


            //    //assetContainer.FetchAttributes();
            //    // Get hold of the destination blobs
            //    var blobs = assetContainer.ListBlobs();
            //    log.Info($"blobs retrieved");


            //    //foreach (CloudBlockBlob blob in blobs)
            //    //{

            //   // CloudBlockBlob blob = assetContainer.GetBlockBlobReference(asset.Name);
            //   // blob.FetchAttributes();
            //    log.Info($"blobs retrieved {blob.Uri} {blob.Name} {blob.Properties.Length}");

            //        //var assetFile = await asset.AssetFiles.CreateAsync(blob.Name, CancellationToken.None);
            //        assetFile.ContentFileSize = blob.Properties.Length;
            //        //assetFile.IsPrimary = true;
            //        assetFile.Update();
            //        log.Info($"Daves Asset file updated : {assetFile.Name}");

            //    //}

            //    asset.Update();

            //    log.Info("Asset updated");
            //}
            //catch (Exception ex)
            //{
            //    log.Info($"Exception {ex}");
            //    return req.CreateResponse(HttpStatusCode.InternalServerError, new
            //    {
            //        Error = ex.ToString()
            //    });
            //}


            //return req.CreateResponse(HttpStatusCode.OK);
        }

    }
}













