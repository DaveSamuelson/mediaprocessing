using Microsoft.Azure;
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