﻿using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WhatsIn.Helpers;
using WhatsIn.Models;
using WhatsIn.Services;
using WhatsIn.Services.Models;

namespace WhatsIn.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class ProductController : ControllerBase
    {
        private IProductsService _products;
        private IPlaces _places;
        private IPosts _posts;
        private readonly IWebHostEnvironment _environment;

        public ProductController(IProductsService products, IPlaces places, IPosts posts, IWebHostEnvironment environment)
        {
            _products = products;
            _places = places;
            _posts = posts;
            _environment = environment;
        }


        /// <summary>
        /// <c>UploadImage</c> extracts GPS coordinates from uploaded image.
        /// If successful, it stores the image and returns the coordinates and unique filename
        /// Route: <c>/Product/UploadImage</c>
        /// </summary>
        /// <param name="fileToUpload"></param>
        /// <returns>
        /// JSON
        ///
        /// <example>
        /// <code>
        /// {"FileName":"637223858708524907.jpg","Latitude":51.49886388888889,"Longitude":-2.5948444444444445}
        /// </code>
        /// </example>
        /// </returns>
        [HttpPost]
        [EnableCors("WhatsInPolicy")]
        public IActionResult UploadImage(IFormFile fileToUpload)
        {
            if (fileToUpload == null)
                return BadRequest("No image");

            try
            {
                var gps = ImageMetadataReader.ReadMetadata(fileToUpload.OpenReadStream())
                                 .OfType<GpsDirectory>()
                                 .FirstOrDefault();

                if (gps == null)
                {
                    return new StatusCodeResult(StatusCodes.Status422UnprocessableEntity);
                }

                var imageId = DateTime.Now.Ticks;
                var ext = Path.GetExtension(fileToUpload.FileName);
                var fileName = imageId + ext;

                GeoLocation location = gps.GetGeoLocation();

                var uploadPath = Path.Combine(_environment.ContentRootPath, "ImageUploads");
                var filePath = Path.Combine(uploadPath, fileName);

                var imageResult = new ImageResult()
                {
                    FileName = fileName,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude
                };

                // FIXME we have a scenario here where images can get abandoned if an Add request isn't made
                // Wanted to avoid uploading an image more than once
                using (var inStream = fileToUpload.OpenReadStream())
                using (var image = Image.Load(inStream, out IImageFormat format))
                {
                    image.Mutate(
                        i => i.Resize(200, 200));

                    image.Save(filePath);
                }

                return Ok(JsonConvert.SerializeObject(imageResult));
            }
            catch (Exception e)
            {
                // log e
                return BadRequest(e.Message);
            }
        }


        /// <summary>
        /// <c>Add</c> Adds a product and place to the database
        /// Route: <c>/Product/Add</c>
        /// </summary>
        /// <param name="productName"></param>
        /// <param name="placeName"></param>
        /// <param name="placeLatitude"></param>
        /// <param name="placeLongitude"></param>
        /// <param name="fileName"></param>
        /// <returns>Status code</returns>
        public IActionResult Add(string productName, string placeName, double? placeLatitude, double? placeLongitude, string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(productName) || string.IsNullOrWhiteSpace(placeName))
                {
                    return BadRequest();
                }

                if (!LocationHelper.IsValidLocation(placeLatitude, placeLongitude))
                {
                    return BadRequest();
                }

                var place = _places.GetPlace(placeName);
                if (place == null)
                {
                    place = _places.AddPlace(placeName, placeLatitude.Value, placeLongitude.Value);
                }

                var product = _products.GetProduct(productName);
                if (product == null)
                {
                    product = _products.AddProduct(productName);
                };

                var post = _posts.Add(product.Id, place.Id, fileName);

                place.Posts.ToList().Add(post);
                product.Posts.ToList().Add(post);

                _places.UpdatePlace(place);
                _products.UpdateProduct(product);

                return Ok();
            }
            catch(Exception e)
            {
                // log e
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }


        /// <summary>
        /// <c>FindProducts</c> performs a wildcard search on product names
        /// Route: <c>/Product/FindProducts</c>
        /// </summary>
        /// <param name="productName"></param>
        /// <returns>
        /// JSON collection of places with matching products
        ///
        /// <example>
        /// <code>
        /// ?productName=bread
        ///
        /// [{"PlaceName":"The Food Shop","ProductName":"bread","Latitude":51.458068,"Longitude":-2.591259,"ImageHref":""},
        /// {"PlaceName":"The Other Food Shop","ProductName":"loaf of bread","Latitude":51.458068,"Longitude":-2.591259,"ImageHref":"637223858708524907.jpg"}]
        /// </code>
        /// </example>
        /// </returns>
        public IActionResult FindProducts(string productName)
        {
            IEnumerable<int> productIds = _products.GetWildCardIds(productName);
            IEnumerable<Post> posts = _posts.GetProductPosts(productIds);

            var results = new List<SearchResult>();

            // FIXME this is inefficient
            foreach (var post in posts.ToList())
            {
                var place = _places.GetPlace(post.PlaceId);
                var product = _products.GetProduct(post.ProductId);

                var imageHref = "";
                if (!string.IsNullOrWhiteSpace(post.ImageFileName))
                {
                    imageHref = $"{Request.Scheme}://{Request.Host}/ImageUploads/{post.ImageFileName}";
                }

                results.Add(new SearchResult()
                {
                    PlaceName = place.Name,
                    ProductName = product.Name,
                    Longitude = place.Longitude,
                    Latitude = place.Latitude,
                    ImageHref = imageHref
                });
            }

            return Ok(JsonConvert.SerializeObject(results));
        }
    }
}
