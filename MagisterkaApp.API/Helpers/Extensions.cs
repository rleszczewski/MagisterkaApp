using System;
using Microsoft.AspNetCore.Http;

namespace MagisterkaApp.API.Helpers
{
    public static class Extensions
    {
        public static void AddApplicationError(this HttpResponse response, string message)
        {
            //adding new header to the client called aplication-error
            //which have error message as it's value
            response.Headers.Add("Application-Error", message);
            //these 2 headers simply allow to get error to application-erorr header
            response.Headers.Add("Access-Control-Expose-Headers","Application-Error");
            response.Headers.Add("Access-Control-Allow-Origin","*");

        }

        public static int CalculateAge(this DateTime dateTime)
        {   
            int age = DateTime.Now.Year - dateTime.Year;
            var dateNow = DateTime.Now;

            

            if(dateTime.AddYears(age)>DateTime.Now)
            {
               age--;
            }
            
            return age;
            
        }


    }
}