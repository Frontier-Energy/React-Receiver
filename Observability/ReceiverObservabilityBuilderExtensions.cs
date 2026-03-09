using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace React_Receiver.Observability;

public static class ReceiverObservabilityBuilderExtensions
{
    public static WebApplicationBuilder AddReceiverObservability(this WebApplicationBuilder builder)
    {
        builder.Logging.Configure(options =>
        {
            options.ActivityTrackingOptions =
                ActivityTrackingOptions.SpanId |
                ActivityTrackingOptions.TraceId |
                ActivityTrackingOptions.ParentId |
                ActivityTrackingOptions.Tags |
                ActivityTrackingOptions.Baggage;
        });

        return builder;
    }
}
