using System;
namespace ImageProcessorWebApp.Models
{

	public class UploadModel
	{
		public IFormFile File { get; set; }
		public orientation Orientation { get; set; }
		public imageType ImageType { get; set; }
    }
}

