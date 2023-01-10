using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ImageProcessorWebApp.Models;
 
 
using System.ComponentModel;
 
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using ImageProcessorWebApp.Repositories;

namespace AzureImageProcessor.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
  
    public IImageProcessorRepo IImageProcessorRepo { get; set; }

    public HomeController(ILogger<HomeController> logger,
    IImageProcessorRepo imageProcessorRepo)
    {
        _logger = logger;
   
        IImageProcessorRepo = imageProcessorRepo;
    }

    public IActionResult Index()
    {

        return View();
    }


    [HttpPost]
    public  async Task<IActionResult> UploadImage(UploadModel model)
    {

        //file size check
        var imageSizeMB = model.File.Length / (1024.0f * 1024.0f);
        if (imageSizeMB > 0.1)
        {
            ModelState.AddModelError("Error", "* Size must be less than 5 MB !");
            return View("Index");
        }

        //image file type check
        if (!model.File.ContentType.StartsWith("image/"))
        {
            ModelState.AddModelError("Error", "* File must be an image !");
            return View("Index");
        }

        //Process image and perform resulted images download to the storage container
       var resultBlobs=  await IImageProcessorRepo.ProcessUploadImages(model.File, model.ImageType , model.Orientation);

        return RedirectToAction("DownloadImages", new { resultBlobs , model.Orientation});
    }

    [HttpGet]
    public IActionResult DownloadImages(List<string> resultBlobs, orientation orientation)
    {
        ViewData["resolutions"] = IImageProcessorRepo.GetResolutions();
        ViewData["orientation"] = orientation;
        return View(resultBlobs);
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
}

