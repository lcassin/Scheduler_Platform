using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SchedulerPlatform.API.Filters;

public class ModelStateLoggingFilter : IAlwaysRunResultFilter
{
    private readonly ILogger<ModelStateLoggingFilter> _logger;

    public ModelStateLoggingFilter(ILogger<ModelStateLoggingFilter> logger)
    {
        _logger = logger;
    }

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is BadRequestObjectResult badRequestResult)
        {
            if (badRequestResult.Value is ValidationProblemDetails validationProblem)
            {
                var errors = validationProblem.Errors
                    .SelectMany(kvp => kvp.Value.Select(errorMsg => new
                    {
                        Field = kvp.Key,
                        Error = errorMsg
                    }))
                    .ToList();

                _logger.LogError("ModelState validation failed for {ActionName}. Error count: {ErrorCount}. Errors: {@ValidationErrors}", 
                    context.ActionDescriptor.DisplayName, errors.Count, errors);
            }
            else
            {
                _logger.LogError("Bad request returned for {ActionName}. Result value: {@ResultValue}", 
                    context.ActionDescriptor.DisplayName, badRequestResult.Value);
            }
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
