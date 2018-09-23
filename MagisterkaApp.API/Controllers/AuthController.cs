
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using AutoMapper;
using MagisterkaApp.API.Data;
using MagisterkaApp.API.Dtos;
using MagisterkaApp.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace MagisterkaApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthRepository _repo;

        public readonly IConfiguration _config;

        private readonly IMapper _mapper;

        public AuthController(IAuthRepository repo, IConfiguration config, IMapper mapper)
        {
            _mapper = mapper;
            _repo = repo;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(UserRegisterDto userForRegisterDto )
        {
            //validate request
            // if(!ModelState.IsValid)
            //     return BadRequest(ModelState);

            userForRegisterDto.Username = userForRegisterDto.Username.ToLower();
            if(await _repo.UserExists(userForRegisterDto.Username))
            {
                return BadRequest("User already exsits");
            }

            var userToCreate = _mapper.Map<User>(userForRegisterDto);

            var createdUser = await _repo.Register(userToCreate, userForRegisterDto.Password);

            var userToReturn = _mapper.Map<UserForDetailedDto>(createdUser);

            return CreatedAtRoute("GetUser", new {controller = "Users", id = createdUser.Id}, userToReturn);
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login(UserForLoginDto userForLoginDto )
        {
            
            //checking if user exist
            var userFromRepo = await  _repo.Login(userForLoginDto.Username.ToLower(), userForLoginDto.Password);

            if(userFromRepo == null)
            return Unauthorized();

            //building a token
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userFromRepo.Id.ToString()),
                new Claim(ClaimTypes.Name, userFromRepo.Username)
            };

            //key to sign token which is stored in appsettings
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config
                            .GetSection("AppSettings:Token").Value));
            
            
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = System.DateTime.Now.AddDays(1),
                SigningCredentials = creds
            };

            var tokenHandler = new JwtSecurityTokenHandler();

            var token = tokenHandler.CreateToken(tokenDescriptor);

            // trying to make img near Welcome User

            var user=_mapper.Map<UserForListDto>(userFromRepo);

            return Ok(
                new {
                    token = tokenHandler.WriteToken(token),
                    user
                }
            );
        }

    }
}
            