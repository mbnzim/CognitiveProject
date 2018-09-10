using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Azure;


namespace Intellipix.Models
{
    public class BlobInfo
    {
        public string ImageUri { get; set; }
        public string ThumbnailUri { get; set; }
        public string Caption { get; set; }
    }


   

    public class BlobBusiness
    {
        public bool DeleteBlob(string name, string containername)
        {
            var container = GetBlobContainer(containername);
            //  var fileName = Path.GetFileName(file.FileName);
            var blockBlob = container.GetBlockBlobReference(name);
            //delete blob from container    
            bool delete = blockBlob.DeleteIfExists();

            return delete;
            /* var container = blobClient.GetContainerReference(containerName);
             var blockBlob = container.GetBlockBlobReference(fileName);
             return blockBlob.DeleteIfExists();*/
        }

        public CloudBlobContainer GetBlobContainer(string containername)
        {
            // Retrieve storage account from connection string.
            var storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));

            // Create the blob client.
            var blobClient = storageAccount.CreateCloudBlobClient();

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