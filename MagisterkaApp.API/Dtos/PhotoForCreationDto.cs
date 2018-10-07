using System;
using Microsoft.AspNetCore.Http;

namespace MagisterkaApp.API.Dtos
{
    public class PhotoForCreationDto
    {
        public string Url { get; set; }
        public IFormFile File { get; set; }
        public string Description { get; set; }
        public DateTime DateAdded { get; set; }
        public string PublicId { get; set; }
        public Boolean IsFirstPhoto {get; set; }

        public string Confidence {get; set;}


        public PhotoForCreationDto()
        {
            DateAdded = DateTime.Now;
        }

    

    }
}