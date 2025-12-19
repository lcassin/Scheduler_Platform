// Custom JS interop for Plotly chart click events
// This bypasses Plotly.Blazor's built-in event handling which has timing issues

window.chartInterop = {
    // Store references to avoid duplicate subscriptions
    _subscriptions: new Map(),

    // Subscribe to plotly_click event on a chart element
    subscribeToClick: function (dotNetRef, wrapperId) {
        // Remove any existing subscription for this chart
        this.unsubscribeFromClick(wrapperId);

        // Wait for the chart element to be ready with retry logic
        const maxRetries = 30;
        let retryCount = 0;

        const trySubscribe = () => {
            const wrapperElement = document.getElementById(wrapperId);
            
            if (!wrapperElement) {
                if (retryCount < maxRetries) {
                    retryCount++;
                    setTimeout(trySubscribe, 150);
                }
                return;
            }

            // Find the actual Plotly chart element inside the wrapper
            // Plotly creates elements with class 'js-plotly-plot' or 'plotly-graph-div'
            let plotlyElement = wrapperElement.querySelector('.js-plotly-plot');
            if (!plotlyElement) {
                plotlyElement = wrapperElement.querySelector('[class*="plotly"]');
            }
            // Also check if the wrapper itself might be the plotly element
            if (!plotlyElement && wrapperElement.classList.contains('js-plotly-plot')) {
                plotlyElement = wrapperElement;
            }
            
            if (!plotlyElement) {
                if (retryCount < maxRetries) {
                    retryCount++;
                    setTimeout(trySubscribe, 150);
                }
                return;
            }

            // Check if Plotly has initialized this element (it should have 'on' method)
            if (!plotlyElement.on) {
                if (retryCount < maxRetries) {
                    retryCount++;
                    setTimeout(trySubscribe, 150);
                }
                return;
            }

            // Create the click handler
            const clickHandler = function (data) {
                if (!data || !data.points || data.points.length === 0) {
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

                // Call the .NET method
                dotNetRef.invokeMethodAsync('OnChartClickFromJs', points)
                    .catch(err => console.error('[chartInterop] .NET callback failed:', err));
            };

            // Subscribe to the event
            plotlyElement.on('plotly_click', clickHandler);

            // Store the subscription for cleanup
            this._subscriptions.set(wrapperId, {
                element: plotlyElement,
                handler: clickHandler,
                dotNetRef: dotNetRef
            });
        };

        // Start the subscription attempt
        trySubscribe();
    },

    // Unsubscribe from plotly_click event
    unsubscribeFromClick: function (chartId) {
        const subscription = this._subscriptions.get(chartId);
        if (subscription) {
            try {
                if (subscription.element && subscription.element.removeListener) {
                    subscription.element.removeListener('plotly_click', subscription.handler);
                }
            } catch (e) {
                // Ignore cleanup errors
            }
            this._subscriptions.delete(chartId);
        }
    },

    // Dispose all subscriptions (called when component is disposed)
    disposeAll: function () {
        for (const chartId of this._subscriptions.keys()) {
            this.unsubscribeFromClick(chartId);
        }
    }
};
