using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UniVerein.Api.ApiResults.ErrorResults;
using UniVerein.Api.Exceptions;

namespace UniVerein.Api.Extensions;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DisabledInDemoAttribute : Attribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        IConfiguration configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        if (!DemoMode.IsEnabled(configuration))
        {
            await next();
            return;
        }

        ForbiddenRequestResult error = new(
            errorMessage: "Not available in the demo.",
            moreInfo: "This function is disabled in the demo version.")
        {
            ErrorCode = ApiErrorCodes.DEMO_MODE_DISABLED
        };
        context.Result = new ObjectResult(error) { StatusCode = error.StatusCode };
    }
}
