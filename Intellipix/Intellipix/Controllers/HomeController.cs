using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Intellipix.Models;

using System.Drawing;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;

using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage;
using Microsoft.Extensions.Configuration;

using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;

namespace Intellipix.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;

        public HomeController(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public ActionResult Index(string id)
        {
            // Pass a list of blob URIs and captions in ViewBag
            CloudStorageAccount account = CloudStorageAccount.Parse(_configuration.GetConnectionString("Storage"));
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

        private bool HasMatchingMetadata(CloudBlockBlob blob, string term)
        {
            foreach (var item in blob.Metadata)
            {
                if (item.Key.StartsWith("Tag") && item.Value.Equals(term, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }

            return false;
        }

        [HttpPost]
        public ActionResult Search(string term)
        {
            return RedirectToAction("Index", new { id = term });
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        public async Task<ActionResult> Upload(IFormFile file)
        {
            if (file?.Length > 0)
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
                        CloudStorageAccount account = CloudStorageAccount.Parse(_configuration.GetConnectionString("Storage"));
                        CloudBlobClient client = account.CreateCloudBlobClient();
                        CloudBlobContainer container = client.GetContainerReference("photos");
                        CloudBlockBlob photo = container.GetBlockBlobReference(Path.GetFileName(file.FileName));
                        await photo.UploadFromStreamAsync(file.OpenReadStream());

                        // Generate a thumbnail and save it in the "thumbnails" container
                        using (var formFileStream = file.OpenReadStream())
                        using (var sourceImage = Image.FromStream(formFileStream))
                        {
                            var newWidth = 192;
                            var newHeight = (Int32)(1.0 * sourceImage.Height / sourceImage.Width * newWidth);
                            using (var destinationImage = new Bitmap(sourceImage, new Size(newWidth, newHeight)))
                            using (var stream = new MemoryStream())
                            {
                                destinationImage.Save(stream, sourceImage.RawFormat);
                                stream.Seek(0L, SeekOrigin.Begin);
                                container = client.GetContainerReference("thumbnails");
                                CloudBlockBlob thumbnail = container.GetBlockBlobReference(Path.GetFileName(file.FileName));
                                await thumbnail.UploadFromStreamAsync(stream);
                            }
                        }

                        // Submit the image to Azure's Computer Vision API
                        ComputerVisionClient vision = new ComputerVisionClient(
                            new ApiKeyServiceClientCredentials(_configuration.GetSection("Vision").GetValue<String>("Key")),
                            new System.Net.Http.DelegatingHandler[] { }
                        );

                        vision.Endpoint = _configuration.GetSection("Vision").GetValue<String>("Endpoint");

                        VisualFeatureTypes[] features = new VisualFeatureTypes[] { VisualFeatureTypes.Description };
                        ImageAnalysis result = await vision.AnalyzeImageAsync(photo.Uri.ToString(), features);

                        // Record the image description and tags in blob metadata
                        photo.Metadata.Add("Caption", result.Description.Captions[0].Text);

                        for (int i = 0; i < result.Description.Tags.Count; i++)
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
    }
}
