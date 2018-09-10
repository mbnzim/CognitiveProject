using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ImageResizer;
using Intellipix.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Configuration;
using System.Threading.Tasks;
using System.IO;
using Microsoft.ProjectOxford.Vision;
using Microsoft.AspNetCore.Mvc;
using System.Web.Caching;
//using System.Web.Mvc;

namespace Intellipix.Controllers
{
    [ResponseCache(Duration = 30)]
    public class HomeController : System.Web.Mvc.Controller
    {
        //======================Get Image==================
        #region index
        public System.Web.Mvc.ActionResult Index(string id)
        {
            // Pass a list of blob URIs and captions in ViewBag
            CloudStorageAccount account = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference("photos");
            List<BlobInfo> blobs = new List<BlobInfo>();

            foreach (IListBlobItem item in container.ListBlobs())
            {
                var blob = item as CloudBlockBlob;
                if (blob != null)
                {
                    blob.FetchAttributes(); // Get blob metadata
                    if (String.IsNullOrEmpty(id) || HasMatchingMetadata(blob, id))
                    {
                        var caption = blob.Metadata.ContainsKey("Caption") ? blob.Metadata["Caption"] : blob.Name;
                        blobs.Add(new BlobInfo()
                        {
                            ImageUri = blob.Uri.ToString(),
                            ThumbnailUri = blob.Uri.ToString().Replace("/photos/", "/thumbnails/"),
                            Caption = caption
                        });
                    }
                }
            }

            ViewBag.Blobs = blobs.ToArray();
            ViewBag.Search = id; // Prevent search box from losing its content
            return View();
        }
        #endregion index

        //======================Uploading Image===================
        #region upload image
        //Cach response https://jakeydocs.readthedocs.io/en/latest/performance/caching/response.html
        [ResponseCache(Duration = 60)]
        [System.Web.Mvc.HttpPost]
        public async Task<System.Web.Mvc.ActionResult> Upload(HttpPostedFileBase file)
        {
            if (file != null && file.ContentLength > 0)
            {
                // Make sure the user selected an image file
                if (!file.ContentType.StartsWith("image"))
                {
                    TempData["Message"] = "Only image files may be uploaded";
                }
                else
                {
                    try
                    {
                        // Save the original image in the "photos" container
                        CloudStorageAccount account = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
                        CloudBlobClient client = account.CreateCloudBlobClient();
                        CloudBlobContainer container = client.GetContainerReference("photos");
                        CloudBlockBlob photo = container.GetBlockBlobReference(Path.GetFileName(file.FileName));
                        await photo.UploadFromStreamAsync(file.InputStream);

                        // Generate a thumbnail and save it in the "thumbnails" container
                        using (var outputStream = new MemoryStream())
                        {
                            file.InputStream.Seek(0L, SeekOrigin.Begin);
                            var settings = new ResizeSettings { MaxWidth = 192 };
                            ImageBuilder.Current.Build(file.InputStream, outputStream, settings);
                            outputStream.Seek(0L, SeekOrigin.Begin);
                            container = client.GetContainerReference("thumbnails");
                            CloudBlockBlob thumbnail = container.GetBlockBlobReference(Path.GetFileName(file.FileName));
                            await thumbnail.UploadFromStreamAsync(outputStream);
                        }
                        // Submit the image to Azure's Computer Vision API
                        VisionServiceClient vision = new VisionServiceClient(
                            ConfigurationManager.AppSettings["SubscriptionKey"],
                            ConfigurationManager.AppSettings["VisionEndpoint"]
                        );

                        VisualFeature[] features = new VisualFeature[] { VisualFeature.Description };
                        var result = await vision.AnalyzeImageAsync(photo.Uri.ToString(), features);

                        // Record the image description and tags in blob metadata
                        photo.Metadata.Add("Caption", result.Description.Captions[0].Text);

                        for (int i = 0; i < result.Description.Tags.Length; i++)
                        {
                            string key = String.Format("Tag{0}", i);
                            photo.Metadata.Add(key, result.Description.Tags[i]);
                        }

                        await photo.SetMetadataAsync();
                    }
                    catch (Exception ex)
                    {
                        // In case something goes wrong
                        TempData["Message"] = ex.Message;
                    }
                }
            }

            return RedirectToAction("Index");
        }
        #endregion upload image

        //======================Search Image==================

        #region Search for Image
        //Cach response  https://jakeydocs.readthedocs.io/en/latest/performance/caching/response.html
        [ResponseCache(Duration = 60)]
        [System.Web.Mvc.HttpPost]
        public System.Web.Mvc.ActionResult Search(string term)
        {
            return RedirectToAction("Index", new { id = term });
        }
        private bool HasMatchingMetadata(CloudBlockBlob blob, string term)
        {
            foreach (var item in blob.Metadata)
            {
                if (item.Key.StartsWith("Tag") && item.Value.Equals(term, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }

            return false;
        }
        #endregion search for Image 


        //======================Getting the Image Properties==================
        #region Getting the Image Properties
        //Cach response  https://jakeydocs.readthedocs.io/en/latest/performance/caching/response.html
        [ResponseCache(Duration = 60)]
        public System.Web.Mvc.ActionResult getPropeties(string name)
        {
            Uri uri = new Uri(name);
            CloudStorageAccount account = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference("photos");
            string filename = System.IO.Path.GetFileName(uri.LocalPath);

            var blob = container.GetBlockBlobReference(filename);
            String URL = blob.Uri.AbsolutePath.ToString();

            //Fetching Properties from azure storage
            blob.FetchAttributes();
            ViewBag.URL = uri.ToString();
            ViewBag.blobname = blob.Name;
            ViewBag.Legnth = blob.Properties.Length;
            ViewBag.BlobType = blob.BlobType;
            ViewBag.ETag = blob.Properties.ETag;
            ViewBag.Created = blob.Properties.Created;
            ViewBag.LastModified = blob.Properties.LastModified;

            return View();
        }
        #endregion Getting the Image Properties

        //=======================Delete Blob================
        #region Delete Blob
        // deleting a blob https://www.c-sharpcorner.com/article/upload-download-and-delete-blob-files-in-azure-storage/
        [ResponseCache(Duration = 10, Location = ResponseCacheLocation.Any, NoStore = true)]
        public System.Web.Mvc.ActionResult RemoveBlob(string name)
        {
            Uri uri = new Uri(name);
            CloudStorageAccount account = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference("photos");
            string filename = System.IO.Path.GetFileName(uri.LocalPath);
            var blob = container.GetBlockBlobReference(filename);
            blob.FetchAttributes();

            BlocStorageService blobbusiness = new BlocStorageService();
            bool isDeleted = blobbusiness.DeleteBlob(blob.Name, "photos");

            return RedirectToAction("Index");
        }
        #endregion
    }
}