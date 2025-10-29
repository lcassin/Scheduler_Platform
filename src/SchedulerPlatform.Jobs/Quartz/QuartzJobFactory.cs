using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Spi;

namespace SchedulerPlatform.Jobs.Quartz;

public class QuartzJobFactory : IJobFactory
{
    private readonly IServiceProvider _serviceProvider;

    public QuartzJobFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        var jobType = bundle.JobDetail.JobType;
        
        var scope = _serviceProvider.CreateScope();
        
        var job = (IJob)scope.ServiceProvider.GetRequiredService(jobType);
        
        bundle.JobDetail.JobDataMap.Put("scope", scope);
        
        return job;
    }

    public void ReturnJob(IJob job)
    {
        if (job is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
