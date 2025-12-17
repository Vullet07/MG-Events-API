using Azure;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Extensions
{
    [ApiController]
    public abstract class ApiControllerBase : ControllerBase
    {
        protected IActionResult ToApiValidationSuccess<T>(T data, string? message = null)
            => Ok(ApiResponse<T>.Ok(data, message));

        protected IActionResult ToApiValidationSuccess(string? message = null)
            => Ok(ApiResponse.Ok(message));

        protected IActionResult ToApiValidationFail(string message, int statusCode = 400)
        {
            Response.StatusCode = statusCode;
            return Ok(ApiResponse.Fail(message));
        }
    }
}
