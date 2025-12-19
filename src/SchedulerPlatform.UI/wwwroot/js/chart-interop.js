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
                console.log(`[chartInterop] Wrapper element '${wrapperId}' not found, retry ${retryCount + 1}/${maxRetries}`);
                if (retryCount < maxRetries) {
                    retryCount++;
                    setTimeout(trySubscribe, 150);
                } else {
                    console.error(`[chartInterop] Failed to find wrapper element '${wrapperId}' after ${maxRetries} retries`);
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
                console.log(`[chartInterop] Plotly element not found inside '${wrapperId}', retry ${retryCount + 1}/${maxRetries}`);
                if (retryCount < maxRetries) {
                    retryCount++;
                    setTimeout(trySubscribe, 150);
                } else {
                    console.error(`[chartInterop] Failed to find Plotly element inside '${wrapperId}' after ${maxRetries} retries`);
                    // Log what we found for debugging
                    console.log(`[chartInterop] Wrapper innerHTML:`, wrapperElement.innerHTML.substring(0, 500));
                }
                return;
            }

            // Check if Plotly has initialized this element (it should have 'on' method)
            if (!plotlyElement.on) {
                console.log(`[chartInterop] Plotly element inside '${wrapperId}' not initialized yet (no 'on' method), retry ${retryCount + 1}/${maxRetries}`);
                if (retryCount < maxRetries) {
                    retryCount++;
                    setTimeout(trySubscribe, 150);
                } else {
                    console.error(`[chartInterop] Plotly element inside '${wrapperId}' never got 'on' method after ${maxRetries} retries`);
                    console.log(`[chartInterop] Element classes:`, plotlyElement.className);
                }
                return;
            }

            console.log(`[chartInterop] Found Plotly element inside '${wrapperId}', subscribing to plotly_click`);

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
            plotlyElement.on('plotly_click', clickHandler);

            // Store the subscription for cleanup
            this._subscriptions.set(wrapperId, {
                element: plotlyElement,
                handler: clickHandler,
                dotNetRef: dotNetRef
            });

            console.log(`[chartInterop] Successfully subscribed to plotly_click on '${wrapperId}'`);
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
