﻿using Microsoft.Azure;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MediaFunctions
{

    public class CreateMediaAsset
    {
        
        private static readonly string _mediaServicesAccountName = Environment.GetEnvironmentVariable("AMSAccount");
        private static readonly string _mediaServicesAccountKey = Environment.GetEnvironmentVariable("AMSKey");

        static string _storageAccountName = Environment.GetEnvironmentVariable("MediaServicesStorageAccountName");
        static string _storageAccountKey = Environment.GetEnvironmentVariable("MediaServicesStorageAccountKey");

        // Field for service context.
        private static CloudMediaContext _context = null;
        private static MediaServicesCredentials _cachedCredentials = null;
        private static CloudStorageAccount _storageAccount = null;
        private static CloudStorageAccount _destinationStorageAccount = null; 



        public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
        {
            log.Info($"C# HTTP trigger function processed a request. RequestUri={req.RequestUri}");

            string jsonContent = await req.Content.ReadAsStringAsync();
            dynamic data = JsonConvert.DeserializeObject(jsonContent);

            log.Info(jsonContent);
            if (data.assetName == null)
            {
                return  req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass assetName in the input object"
                });
            }

            string assetName = data.assetName;
            log.Info($"Using Azure Media Services account : {_mediaServicesAccountName}");
            IAsset newAsset = null;

            try
            {
                // Create and cache the Media Services credentials in a static class variable.
                _cachedCredentials = new MediaServicesCredentials(
                                _mediaServicesAccountName,
                                _mediaServicesAccountKey);

                // Used the chached credentials to create CloudMediaContext.
                _context = new CloudMediaContext(_cachedCredentials);

               
                _storageAccount =  new CloudStorageAccount(new StorageCredentials(_storageAccountName, _storageAccountKey), true);
                CloudBlobClient blobClient = _storageAccount.CreateCloudBlobClient();

                
                newAsset = _context.Assets.Create(assetName, AssetCreationOptions.None);
                log.Info($"Asset Created : {newAsset.Name} {newAsset.Uri}");

                CloudBlobClient sourceCloudBlobClient =
                _storageAccount.CreateCloudBlobClient();


                log.Info($"About to get source container reference: InputMediaContainer {Environment.GetEnvironmentVariable("InputMediaContainer")}");
                CloudBlobContainer sourceContainer =
                sourceCloudBlobClient.GetContainerReference(Environment.GetEnvironmentVariable("InputMediaContainer"));
                log.Info($"Got soucontainer {sourceContainer.Uri}");


                log.Info($"About to get block blob referenece");
                CloudBlockBlob sourceBlob2 = sourceContainer.GetBlockBlobReference(assetName);
                //IAsset asset2 = _context.Assets.CreateFromBlob(sourceBlob2, new StorageCredentials(_storageAccountName, _storageAccountKey), AssetCreationOptions.None);
                log.Info($"Got block blob reference {sourceBlob2.Uri}");



                log.Info($"About to create desitination storage account");
                // FROM HERE 
                _destinationStorageAccount = new CloudStorageAccount(new StorageCredentials(_storageAccountName,
                   _storageAccountKey), true);
                log.Info($"Destination storage account created");

                CloudBlobClient destBlobStorage = _destinationStorageAccount.CreateCloudBlobClient();


                IAccessPolicy writePolicy = _context.AccessPolicies.Create("writePolicy",
                    TimeSpan.FromHours(24), AccessPermissions.Write);

                ILocator destinationLocator =
                    _context.Locators.CreateLocator(LocatorType.Sas, newAsset, writePolicy);


                log.Info($"About to get destination asset container");
                // Get the asset container URI and Blob copy from mediaContainer to assetContainer. 
                CloudBlobContainer destAssetContainer =
                    destBlobStorage.GetContainerReference((new Uri(destinationLocator.Path)).Segments[1]);
                log.Info($"Got destination asset container {destAssetContainer.Uri}");


                    destAssetContainer.SetPermissions(new BlobContainerPermissions
                    {
                        PublicAccess = BlobContainerPublicAccessType.Container
                    });


                // Get hold of the destination blob

                CloudBlockBlob destBlob = destAssetContainer.GetBlockBlobReference(assetName);
                log.Info($"Dest Blob name: {destBlob.Name} {destBlob.Uri}");

                log.Info($"About to copy to destination blob");
                await destBlob.StartCopyAsync(sourceBlob2);
                log.Info($"BLOB copied");


                destBlob.FetchAttributes();
                var assetFile = newAsset.AssetFiles.Create((sourceBlob2 as ICloudBlob).Name);

                //var blobList = sourceContainer.ListBlobs();

                ////foreach (var sourceBlob in blobList)
                ////{
                //    var assetFile = newAsset.AssetFiles.Create((sourceBlob2 as ICloudBlob).Name);

                //    ICloudBlob destinationBlob = destAssetContainer.GetBlockBlobReference(assetFile.Name);

                //    //// Call the CopyBlobHelpers.CopyBlobAsync extension method to copy blobs.
                //    //using (Task task =
                //    //    CopyBlobHelpers.CopyBlobAsync((CloudBlockBlob)sourceBlob2,
                //    //        (CloudBlockBlob)destinationBlob,
                //    //        new BlobRequestOptions(),
                //    //        CancellationToken.None))
                //    //{
                //    //    task.Wait();
                //    //}

                //    assetFile.ContentFileSize = (sourceBlob2 as ICloudBlob).Properties.Length;
                //    assetFile.Update();
                //    Console.WriteLine("File {0} is of {1} size", assetFile.Name, assetFile.ContentFileSize);
                ////}

                newAsset.Update();

                //destinationLocator.Delete();
                //writePolicy.Delete();























                /// ####################################### FROM HERE
                // IAccessPolicy writePolicy = _context.AccessPolicies.Create("writePolicy", TimeSpan.FromHours(24), AccessPermissions.Write);
                //  ILocator destinationLocator =
                //      _context.Locators.CreateLocator(LocatorType.Sas, newAsset, writePolicy);

                //// Get the asset container URI and Blob copy from mediaContainer to assetContainer. 
                //// Define the destination container and create this if it doesn't exist


                //CloudBlobContainer destAssetContainer = blobClient.GetContainerReference((new Uri(destinationLocator.Path)).Segments[1]);
                //log.Info($"Destination Container is : {destAssetContainer.Name}");
                //if (destAssetContainer.CreateIfNotExists())
                //{
                //    destAssetContainer.SetPermissions(new BlobContainerPermissions
                //    {
                //        PublicAccess = BlobContainerPublicAccessType.Blob
                //    });
                //}

                //// Get source blob from container defined in configuration
                //CloudBlobContainer sourceContainer = blobClient.GetContainerReference(Environment.GetEnvironmentVariable("InputMediaContainer"));
                //ICloudBlob sourceBlob = sourceContainer.GetBlockBlobReference(assetName);




                //// Associate asset file  
                //var assetFile = newAsset.AssetFiles.Create(sourceBlob.Name);
                //ICloudBlob destinationBlob = destAssetContainer.GetBlockBlobReference(assetFile.Name);
                //log.Info($"About to call copy {destinationBlob.Uri}");


                //// Call the CopyBlobHelpers.CopyBlobAsync extension method to copy blobs.
                //using (Task task =
                //    CopyBlobHelpers.CopyBlobAsync((CloudBlockBlob)sourceBlob,
                //        (CloudBlockBlob)destinationBlob,
                //        new BlobRequestOptions(),
                //        CancellationToken.None))
                //{
                //    task.Wait();
                //}






                //log.Info($"About to call copy");
                //log.Info($"Source Blob Absolute URI is {sourceBlob.Uri.AbsoluteUri}");
                //log.Info($"Destination Blob Absolute URI is {destinationBlob.Uri.AbsoluteUri}");
                //((CloudBlob) destinationBlob).StartCopy(new Uri(sourceBlob.Uri.AbsoluteUri));


                //sourceContainer.FetchAttributes();
                //destAssetContainer.FetchAttributes();

                //log.Info($"Dest Blob Properties are : {destinationBlob.Properties.BlobType}");
                //assetFile.ContentFileSize = (sourceBlob as ICloudBlob).Properties.Length;
                //assetFile.Update();

                ////destinationLocator.Delete();
                //writePolicy.Delete();


            }
            catch (Exception ex)
            {
                log.Info($"Exception {ex}");
                return req.CreateResponse(HttpStatusCode.InternalServerError, new
                {
                    Error = ex.ToString()
                });
            }

            log.Info("asset Id: " + newAsset.Id);
            log.Info("container Path: " + newAsset.Uri.Segments[1]);

            return req.CreateResponse(HttpStatusCode.OK, new
            {
                containerPath = newAsset.Uri.Segments[1],
                assetId = newAsset.Id
            });
        }
    }
}