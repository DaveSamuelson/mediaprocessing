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
        private static CloudStorageAccount _storageAccount = null;
        
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
            if (data.assetName == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    error = "Please pass assetName in the input object"
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
                
                // Get the Asset in Media Servs
                string assetid = data.assetId;
                string assetname = data.assetName;
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

                // Create BLOB Client for interacing with Azure Storage
                _storageAccount = new CloudStorageAccount(new StorageCredentials(_storageAccountName, _storageAccountKey), true);
                CloudBlobClient blobClient =  _storageAccount.CreateCloudBlobClient();

                // Get reference to source blob that needs to be assigned to Asset
                CloudBlobContainer sourceContainer = blobClient.GetContainerReference(Environment.GetEnvironmentVariable("InputMediaContainer"));
                CloudBlockBlob sourceBlob = sourceContainer.GetBlockBlobReference(assetname);
                
                IAccessPolicy writePolicy = _context.AccessPolicies.Create("writePolicy",
                    TimeSpan.FromHours(24), AccessPermissions.Write);

                ILocator destinationLocator =
                    _context.Locators.CreateLocator(LocatorType.Sas, asset, writePolicy);


                log.Info($"About to get destination asset container");
                // Get the asset container URI and Blob copy from mediaContainer to assetContainer. 
                CloudBlobContainer destAssetContainer =
                    blobClient.GetContainerReference((new Uri(destinationLocator.Path)).Segments[1]);
                log.Info($"Got destination asset container {destAssetContainer.Uri}");


                destAssetContainer.SetPermissions(new BlobContainerPermissions
                {
                    PublicAccess = BlobContainerPublicAccessType.Container
                });


                // Get hold of the destination blob

                CloudBlockBlob destBlob = destAssetContainer.GetBlockBlobReference(assetname);
                log.Info($"Dest Blob name: {destBlob.Name} {destBlob.Uri}");

                log.Info($"About to copy to destination blob");
                await destBlob.StartCopyAsync(sourceBlob);
                log.Info($"BLOB copied");


                destBlob.FetchAttributes();
                var assetFile = asset.AssetFiles.Create((sourceBlob as ICloudBlob).Name);

               
                assetFile.Update();

                destinationLocator.Delete();
                writePolicy.Delete();




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
            
        }

    }
}













