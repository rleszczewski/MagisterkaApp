using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using MagisterkaApp.API.Data;
using MagisterkaApp.API.Dtos;
using MagisterkaApp.API.Helpers;
using MagisterkaApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using System.Collections.Generic;
using System.Net.Http;
using System.Web;
using System.Text;
using System.Net.Http.Headers;
using Microsoft.ProjectOxford.Face;
using MagisterkApp.API.Helpers;

namespace MagisterkaApp.API.Controllers
{
    [Authorize]
    [Route("api/users/{userId}/photos")]
    public class PhotosController : ControllerBase
    {

        private IHostingEnvironment _enviroment;
        
       // private readonly IFaceServiceClient _faceserviceclient = new FaceServiceClient("203ea128377842d5afef1ffbaa9f3cca", "https://westeurope.api.cognitive.microsoft.com/face/v1.0");
        private readonly IPhotoRepository _repo;
        private readonly IMapper _mapper;
        private readonly IOptions<CognitiveServices> _cognitiveServices;
        private readonly IOptions<CloudinarySettings> _cloudinaryConfig;
        private Cloudinary _cloudinary;

       

        public PhotosController(IPhotoRepository repo, IMapper mapper, IOptions<CloudinarySettings> cloudinaryConfig,IOptions<CognitiveServices> cognitiveServices, IHostingEnvironment environment)
        {
           
            
            _cognitiveServices = cognitiveServices;
            _cloudinaryConfig = cloudinaryConfig;
            _mapper = mapper;
            _repo = repo;
            _enviroment = environment;

          

            Account acc = new Account(
                _cloudinaryConfig.Value.CloudName,
                _cloudinaryConfig.Value.ApiKey,
                _cloudinaryConfig.Value.ApiSecret
            );

            _cloudinary = new Cloudinary(acc);
        }
        [HttpGet("{id}", Name = "GetPhoto")]
        public async Task<IActionResult> GetPhoto(int id)
        {
            var photoFromRepo = await _repo.GetPhoto(id);
            var photo = _mapper.Map<PhotoForReturnDto>(photoFromRepo);

            return Ok(photo);
        }

        [HttpPost]
        public async Task<IActionResult> AddPhotoForUser(int userId, [FromForm]PhotoForCreationDto photoForCreationDto)
        {
            
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            return Unauthorized();

            var userFromRepo = await _repo.GetUser(userId);

            var file = photoForCreationDto.File;

            var uploadResult = new ImageUploadResult();

           
            if(file.Length > 0)
            {
                using( var stream = file.OpenReadStream())
                {
                    var uploadParams = new ImageUploadParams()
                    {
                        //transformation cropping the too big image automaticaly
                        File = new FileDescription(file.Name, stream),
                        Transformation = new Transformation().Width(500).Height(500)
                        .Crop("fill").Gravity("face")
                    };

                    uploadResult = _cloudinary.Upload(uploadParams);

                }
            }

                photoForCreationDto.Url = uploadResult.Uri.ToString();
                string secondPhoto = photoForCreationDto.Url;
                photoForCreationDto.PublicId = uploadResult.PublicId;

                var photo = _mapper.Map<Photo>(photoForCreationDto);

                if(!userFromRepo.Photos.Any(u => u.IsMain))
                {
                    photo.IsMain = true;
                    photo.IsFirstPhoto = true;
                    photo.Confidence = "It's first photo";
                }

            Guid faceid1;
            Guid faceid2;

            if (!photo.IsFirstPhoto)
                {
                    IFaceServiceClient _faceserviceclient = new FaceServiceClient(_cognitiveServices.Value.ServiceKey , _cognitiveServices.Value.ServiceEndPoint);

                    var firstPhoto = userFromRepo.Photos.First(u => u.IsFirstPhoto).Url.ToString();

                    using (Stream faceimagestream = await GetStreamFromUrl(firstPhoto))
                    {
                    var faces = await _faceserviceclient.DetectAsync(faceimagestream, returnFaceId: true);
                    if (faces.Length > 0)
                        faceid1 = faces[0].FaceId;
                    else
                        throw new Exception("No face found in image 1.");
                    }
                    using (Stream faceimagestream = await GetStreamFromUrl(secondPhoto))
                    {
                    var faces = await _faceserviceclient.DetectAsync(faceimagestream, returnFaceId: true);
                    if (faces.Length > 0)
                        faceid2 = faces[0].FaceId;
                    else
                        throw new Exception("No face found in image 1.");
                    }

                    var result = await _faceserviceclient.VerifyAsync(faceid1, faceid2);

                    photo.Confidence = "The Face is identical to first photo in " + result.Confidence.ToString() + "%";

            }

                userFromRepo.Photos.Add(photo);
                
                if (await _repo.SaveAll())
                {
                    var photoToReturn = _mapper.Map<PhotoForReturnDto>(photo);
                    return CreatedAtRoute("GetPhoto", new { id = photo.Id }, photoToReturn);
                }


                return BadRequest("Could not add the photo");
            
        }
        
        [HttpPost("{id}/setMain")]
        public async Task<IActionResult> SetMainPhoto(int userId, int id)
        {
            // checking if the roots the user is accessing matches their user id which matches their token
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            return Unauthorized();

            // getting user from repo
            var user = await _repo.GetUser(userId);
            // checking if there is photo in colection
            if(!user.Photos.Any(p => p.Id == id))
            {
                return Unauthorized();
            }

            var photoFromRepo = await _repo.GetPhoto(id);

            if(photoFromRepo.IsMain)
            return BadRequest("this is already main photo");

            var currentMainPhoto = await _repo.GetMainPhotoForUser(userId);

            currentMainPhoto.IsMain = false;

            photoFromRepo.IsMain = true;

            if(await _repo.SaveAll())
            return NoContent();

            return BadRequest("Could not set photo to main");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePhoto(int userId, int id)
        {

            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            return Unauthorized();

            var user = await _repo.GetUser(userId);
            
            if(!user.Photos.Any(p => p.Id == id))
            {
                return Unauthorized();
            }

            var photoFromRepo = await _repo.GetPhoto(id);

            if(photoFromRepo.IsMain)
            return BadRequest("You cannot delete your main photo");

            if (photoFromRepo.PublicId != null)
            {
                var deleteParams = new DeletionParams(photoFromRepo.PublicId);

                var result = _cloudinary.Destroy(deleteParams);

                if(result.Result == "ok") {
                    _repo.Delete(photoFromRepo);
                }
            }

            if (photoFromRepo.PublicId == null)
            {
                _repo.Delete(photoFromRepo);
            }     

            if(await _repo.SaveAll())
            return Ok();

            return BadRequest("failed to delete the photo");
        }

        private async static Task<Stream> GetStreamFromUrl(string url)
        {
            byte[] imagedata = null;

            using (var wc =  new  System.Net.WebClient())
            {
                imagedata = wc.DownloadData(url);
            }

            return new MemoryStream(imagedata);
        }
    }
}