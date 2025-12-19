// Custom JS interop for Plotly chart click events
// This bypasses Plotly.Blazor's built-in event handling which has timing issues

window.chartInterop = {
    // Store references to avoid duplicate subscriptions
    _subscriptions: new Map(),

    // Subscribe to plotly_click event on a chart element
    subscribeToClick: function (dotNetRef, chartId) {
        // Remove any existing subscription for this chart
        this.unsubscribeFromClick(chartId);

        // Wait for the chart element to be ready with retry logic
        const maxRetries = 20;
        let retryCount = 0;

        const trySubscribe = () => {
            const chartElement = document.getElementById(chartId);
            
            if (!chartElement) {
                console.log(`[chartInterop] Chart element '${chartId}' not found, retry ${retryCount + 1}/${maxRetries}`);
                if (retryCount < maxRetries) {
                    retryCount++;
                    setTimeout(trySubscribe, 100);
                } else {
                    console.error(`[chartInterop] Failed to find chart element '${chartId}' after ${maxRetries} retries`);
                }
                return;
            }

            // Check if Plotly has initialized this element (it should have 'on' method)
            if (!chartElement.on) {
                console.log(`[chartInterop] Chart element '${chartId}' not initialized by Plotly yet, retry ${retryCount + 1}/${maxRetries}`);
                if (retryCount < maxRetries) {
                    retryCount++;
                    setTimeout(trySubscribe, 100);
                } else {
                    console.error(`[chartInterop] Chart element '${chartId}' never got Plotly 'on' method after ${maxRetries} retries`);
                }
                return;
            }

            console.log(`[chartInterop] Subscribing to plotly_click on '${chartId}'`);

            // Create the click handler
            const clickHandler = function (data) {
                console.log('[chartInterop] plotly_click fired!', data);
                
                if (!data || !data.points || data.points.length === 0) {
                    console.log('[chartInterop] No points in click data');
                    return;
                }

                // Map the click data to the format expected by .NET
                const points = data.points.map(function (d) {
                    return {
                        TraceIndex: d.fullData ? d.fullData.index : null,
                        PointIndex: d.pointIndex,
                        PointNumber: d.pointNumber,
                        CurveNumber: d.curveNumber,
                        Text: d.text,
                        X: d.x,
                        Y: d.y,
                        Z: d.z,
                        Lat: d.lat,
                        Lon: d.lon,
                        Label: d.label,
                        Value: d.value,
                        Percent: d.percent
                    };
                });

                console.log('[chartInterop] Invoking .NET callback with points:', points);

                // Call the .NET method
                dotNetRef.invokeMethodAsync('OnChartClickFromJs', points)
                    .then(() => console.log('[chartInterop] .NET callback succeeded'))
                    .catch(err => console.error('[chartInterop] .NET callback failed:', err));
            };

            // Subscribe to the event
            chartElement.on('plotly_click', clickHandler);

            // Store the subscription for cleanup
            this._subscriptions.set(chartId, {
                element: chartElement,
                handler: clickHandler,
                dotNetRef: dotNetRef
            });

            console.log(`[chartInterop] Successfully subscribed to plotly_click on '${chartId}'`);
        };

        // Start the subscription attempt
        trySubscribe();
    },

    // Unsubscribe from plotly_click event
    unsubscribeFromClick: function (chartId) {
        const subscription = this._subscriptions.get(chartId);
        if (subscription) {
            console.log(`[chartInterop] Unsubscribing from plotly_click on '${chartId}'`);
            try {
                if (subscription.element && subscription.element.removeListener) {
                    subscription.element.removeListener('plotly_click', subscription.handler);
                }
            } catch (e) {
                console.log(`[chartInterop] Error removing listener:`, e);
            }
            this._subscriptions.delete(chartId);
        }
    },

    // Dispose all subscriptions (called when component is disposed)
    disposeAll: function () {
        console.log('[chartInterop] Disposing all subscriptions');
        for (const chartId of this._subscriptions.keys()) {
            this.unsubscribeFromClick(chartId);
        }
    }
};
