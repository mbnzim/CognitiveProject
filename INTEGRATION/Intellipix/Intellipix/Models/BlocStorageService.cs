using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace Intellipix.Models
{
    public class BlocStorageService
    {
        //=====================deleting the blob================
        public bool DeleteBlob(string name, string containername)
        {
            var container = GetBlobContainer(containername);
            var blockBlob = container.GetBlockBlobReference(name);
            bool delete = blockBlob.DeleteIfExists();
            return delete;
        }

        //======================Get Blob Container================
        public CloudBlobContainer GetBlobContainer(string containername)
        {
            // Retrieve storage account from connection string.
            CloudStorageAccount account = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);

            // Create the blob client.
            var blobClient = account.CreateCloudBlobClient();

            // Retrieve reference to a previously created container.
            var container = blobClient.GetContainerReference(containername);

            // Set the permissions so the blobs are public. 
            BlobContainerPermissions permissions = new BlobContainerPermissions
            {
                PublicAccess = BlobContainerPublicAccessType.Blob
            };

            //create container if it does not exist
            container.CreateIfNotExists();

            //set permission
            container.SetPermissions(permissions);

            return container;

        }
    }
}